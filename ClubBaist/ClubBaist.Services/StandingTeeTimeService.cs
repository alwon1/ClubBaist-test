using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class StandingTeeTimeService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly TeeTimeBookingService<TKey> _bookingService;

    public StandingTeeTimeService(
        IApplicationDbContext<TKey> dbContext,
        TeeTimeBookingService<TKey> bookingService)
    {
        _dbContext = dbContext;
        _bookingService = bookingService;
    }

    public async Task<StandingTeeTime> RequestAsync(
        Guid seasonId,
        DayOfWeek dayOfWeek,
        TimeOnly slotTime,
        int bookingMemberAccountId,
        List<int> playerMemberAccountIds,
        CancellationToken cancellationToken = default)
    {
        var standing = new StandingTeeTime
        {
            SeasonId = seasonId,
            DayOfWeek = dayOfWeek,
            SlotTime = slotTime,
            BookingMemberAccountId = bookingMemberAccountId,
            PlayerMemberAccountIds = playerMemberAccountIds,
            Status = StandingTeeTimeStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        _dbContext.StandingTeeTimes.Add(standing);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return standing;
    }

    /// <summary>
    /// Approves a pending standing tee time and generates Reservation records for every
    /// matching day-of-week date within the season. Dates where booking rules prevent
    /// creation are skipped. Returns the IDs of successfully created reservations.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ApproveAsync(
        Guid standingTeeTimeId,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var standing = await _dbContext.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.StandingTeeTimeId == standingTeeTimeId, cancellationToken);

        if (standing is null || standing.Status != StandingTeeTimeStatus.Pending)
            return [];

        var season = await _dbContext.Seasons
            .FirstOrDefaultAsync(s => s.SeasonId == standing.SeasonId, cancellationToken);

        if (season is null)
            return [];

        standing.Status = StandingTeeTimeStatus.Approved;
        standing.AdminNote = adminNote;
        standing.ReviewedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate a Reservation for every matching date in the season.
        var createdIds = new List<Guid>();
        var date = season.StartDate;

        while (date <= season.EndDate)
        {
            if (date.DayOfWeek == standing.DayOfWeek)
            {
                var slot = new TeeTimeSlot(
                    date,
                    standing.SlotTime,
                    standing.BookingMemberAccountId,
                    new List<int>(standing.PlayerMemberAccountIds));

                // Re-fetch the reservation ID after creation by querying for the reservation we just made.
                var result = await _bookingService.CreateReservationAsync(slot, cancellationToken);

                if (result >= 0)
                {
                    // Find the reservation just created to capture its ID.
                    var created = await _dbContext.Reservations
                        .Where(r => r.SlotDate == date
                                 && r.SlotTime == standing.SlotTime
                                 && r.BookingMemberAccountId == standing.BookingMemberAccountId
                                 && !r.IsCancelled)
                        .OrderByDescending(r => r.ReservationId)
                        .Select(r => r.ReservationId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (created != Guid.Empty)
                        createdIds.Add(created);
                }
            }

            date = date.AddDays(1);
        }

        return createdIds;
    }

    public async Task<bool> DenyAsync(
        Guid standingTeeTimeId,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var standing = await _dbContext.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.StandingTeeTimeId == standingTeeTimeId, cancellationToken);

        if (standing is null || standing.Status != StandingTeeTimeStatus.Pending)
            return false;

        standing.Status = StandingTeeTimeStatus.Denied;
        standing.AdminNote = adminNote;
        standing.ReviewedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Cancels an approved standing tee time. Does not cancel already-generated Reservation records.
    /// </summary>
    public async Task<bool> CancelAsync(
        Guid standingTeeTimeId,
        CancellationToken cancellationToken = default)
    {
        var standing = await _dbContext.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.StandingTeeTimeId == standingTeeTimeId, cancellationToken);

        if (standing is null || standing.Status == StandingTeeTimeStatus.Cancelled)
            return false;

        standing.Status = StandingTeeTimeStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StandingTeeTime>> GetBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StandingTeeTimes
            .AsNoTracking()
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.SlotTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StandingTeeTime>> GetByMemberAsync(
        int memberAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StandingTeeTimes
            .AsNoTracking()
            .Where(s => s.BookingMemberAccountId == memberAccountId)
            .OrderBy(s => s.SeasonId)
            .ThenBy(s => s.DayOfWeek)
            .ThenBy(s => s.SlotTime)
            .ToListAsync(cancellationToken);
    }
}
