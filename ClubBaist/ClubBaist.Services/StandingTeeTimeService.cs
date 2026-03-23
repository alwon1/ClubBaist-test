using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class StandingTeeTimeService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _db;
    private readonly TeeTimeBookingService<TKey> _bookingService;
    private readonly AvailabilityUpdateService _availabilityUpdates;

    public StandingTeeTimeService(
        IApplicationDbContext<TKey> db,
        TeeTimeBookingService<TKey> bookingService,
        AvailabilityUpdateService availabilityUpdates)
    {
        _db = db;
        _bookingService = bookingService;
        _availabilityUpdates = availabilityUpdates;
    }

    public async Task<StandingTeeTime?> RequestAsync(
        Guid seasonId,
        DayOfWeek dayOfWeek,
        TimeOnly slotTime,
        int bookingMemberAccountId,
        List<int> playerMemberAccountIds,
        CancellationToken cancellationToken = default)
    {
        var memberCategory = await _db.MemberAccounts
            .Where(m => m.MemberAccountId == bookingMemberAccountId)
            .Select(m => (MembershipCategory?)m.MembershipCategory)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberCategory != MembershipCategory.Shareholder)
            return null;

        if (playerMemberAccountIds.Count != 3)
            return null;

        if (playerMemberAccountIds.Distinct().Count() != 3)
            return null;

        if (playerMemberAccountIds.Contains(bookingMemberAccountId))
            return null;

        var alreadyExists = await _db.StandingTeeTimes.AnyAsync(s =>
            s.SeasonId == seasonId &&
            s.DayOfWeek == dayOfWeek &&
            s.SlotTime == slotTime &&
            s.BookingMemberAccountId == bookingMemberAccountId &&
            (s.Status == StandingTeeTimeStatus.Pending || s.Status == StandingTeeTimeStatus.Approved),
            cancellationToken);

        if (alreadyExists)
            return null;

        var stt = new StandingTeeTime
        {
            SeasonId = seasonId,
            DayOfWeek = dayOfWeek,
            SlotTime = slotTime,
            BookingMemberAccountId = bookingMemberAccountId,
            PlayerMemberAccountIds = playerMemberAccountIds,
            Status = StandingTeeTimeStatus.Pending
        };

        _db.StandingTeeTimes.Add(stt);
        await _db.SaveChangesAsync(cancellationToken);
        return stt;
    }

    public async Task<IReadOnlyList<Guid>> ApproveAsync(
        Guid standingTeeTimeId,
        CancellationToken cancellationToken = default)
    {
        var stt = await _db.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.StandingTeeTimeId == standingTeeTimeId, cancellationToken);

        if (stt is null || stt.Status != StandingTeeTimeStatus.Pending)
            return [];

        var season = await _db.Seasons
            .FirstOrDefaultAsync(s => s.SeasonId == stt.SeasonId, cancellationToken);

        if (season is null)
            return [];

        var reservationIds = await CreateReservationsForSttAsync(stt, season, cancellationToken);

        stt.Status = StandingTeeTimeStatus.Approved;
        await _db.SaveChangesAsync(cancellationToken);
        return reservationIds;
    }

    public async Task<bool> DenyAsync(
        Guid standingTeeTimeId,
        CancellationToken cancellationToken = default)
    {
        var stt = await _db.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.StandingTeeTimeId == standingTeeTimeId, cancellationToken);

        if (stt is null || stt.Status != StandingTeeTimeStatus.Pending)
            return false;

        stt.Status = StandingTeeTimeStatus.Denied;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(
        Guid standingTeeTimeId,
        CancellationToken cancellationToken = default)
    {
        var stt = await _db.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.StandingTeeTimeId == standingTeeTimeId, cancellationToken);

        if (stt is null || stt.Status != StandingTeeTimeStatus.Approved)
            return false;

        stt.Status = StandingTeeTimeStatus.Cancelled;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var futureReservations = await _db.Reservations
            .Where(r => r.StandingTeeTimeId == standingTeeTimeId && r.SlotDate > today && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        var affectedDates = new HashSet<DateOnly>();
        foreach (var r in futureReservations)
        {
            r.IsCancelled = true;
            affectedDates.Add(r.SlotDate);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var date in affectedDates)
            _availabilityUpdates.Notify(date);

        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GenerateReservationsForSeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _db.Seasons
            .FirstOrDefaultAsync(s => s.SeasonId == seasonId, cancellationToken);

        if (season is null)
            return new Dictionary<Guid, IReadOnlyList<Guid>>();

        var approvedStts = await _db.StandingTeeTimes
            .Where(s => s.SeasonId == seasonId && s.Status == StandingTeeTimeStatus.Approved)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, IReadOnlyList<Guid>>();
        foreach (var stt in approvedStts)
        {
            var reservationIds = await CreateReservationsForSttAsync(stt, season, cancellationToken);
            result[stt.StandingTeeTimeId] = reservationIds;
        }

        return result;
    }

    public async Task<IReadOnlyList<StandingTeeTime>> GetBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        return await _db.StandingTeeTimes
            .AsNoTracking()
            .Where(s => s.SeasonId == seasonId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StandingTeeTime>> GetByMemberAsync(
        int memberAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _db.StandingTeeTimes
            .AsNoTracking()
            .Where(s => s.BookingMemberAccountId == memberAccountId)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> CreateReservationsForSttAsync(
        StandingTeeTime stt,
        Season season,
        CancellationToken cancellationToken)
    {
        var reservationIds = new List<Guid>();

        for (var date = season.StartDate; date <= season.EndDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != stt.DayOfWeek)
                continue;

            var slot = new TeeTimeSlot(date, stt.SlotTime, stt.BookingMemberAccountId, stt.PlayerMemberAccountIds);
            var (remaining, reservationId) = await _bookingService.CreateReservationAsync(slot, stt.StandingTeeTimeId, cancellationToken);

            if (remaining >= 0 && reservationId.HasValue)
                reservationIds.Add(reservationId.Value);
        }

        return reservationIds;
    }
}
