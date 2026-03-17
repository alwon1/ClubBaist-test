using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class TeeTimeBookingService<TKey> where TKey : IEquatable<TKey>
{
    private const int MaxCapacity = BookingConstants.MaxPlayersPerSlot;

    private readonly IScheduleTimeService _scheduleTimeService;
    private readonly IReadOnlyList<IBookingRule> _rules;
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly AvailabilityUpdateService _availabilityUpdates;

    public TeeTimeBookingService(
        IScheduleTimeService scheduleTimeService,
        IEnumerable<IBookingRule> rules,
        IApplicationDbContext<TKey> dbContext,
        AvailabilityUpdateService availabilityUpdates)
    {
        _scheduleTimeService = scheduleTimeService;
        _rules = rules.ToList();
        _dbContext = dbContext;
        _availabilityUpdates = availabilityUpdates;
    }

    public async Task<IReadOnlyList<DayAvailability>> GetAvailabilityAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Batch: fetch all reservations for the range in one query.
        var reservationsInRange = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.SlotDate >= from && r.SlotDate <= to && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        var occupancyBySlot = reservationsInRange
            .GroupBy(r => (r.SlotDate, r.SlotTime))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(r => r.PlayerMemberAccountIds.Count + 1));

        var days = new List<DayAvailability>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var times = _scheduleTimeService.GetScheduleTimes(date);
            var slots = new List<SlotAvailability>();

            foreach (var time in times)
            {
                occupancyBySlot.TryGetValue((date, time), out var occupancy);
                var context = new BookingEvaluationContext(
                    MemberCategory: null,
                    PrecomputedOccupancy: occupancy);

                var slot = new TeeTimeSlot(date, time, Guid.Empty, []);
                var remaining = Math.Max(0, await EvaluateRulesAsync(slot, context, cancellationToken));
                slots.Add(new SlotAvailability(time, remaining));
            }

            days.Add(new DayAvailability(date, slots));
        }

        return days;
    }

    public async Task<IReadOnlyList<BookedSlot>> GetBookedTimesAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var times = _scheduleTimeService.GetScheduleTimes(date);

        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.SlotDate == date && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        var grouped = reservations
            .GroupBy(r => r.SlotTime)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<BookedSlot>();

        foreach (var time in times)
        {
            grouped.TryGetValue(time, out var slotReservations);
            var playerCount = slotReservations?.Sum(r => r.PlayerMemberAccountIds.Count + 1) ?? 0;
            var remaining = Math.Max(0, MaxCapacity - playerCount);

            result.Add(new BookedSlot(time, remaining, slotReservations ?? []));
        }

        return result;
    }

    public async Task<IReadOnlyList<BookedSlotWithMembers>> GetBookedSlotsWithMembersAsync(
        DateOnly date,
        MembershipCategory? memberCategory = null,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.SlotDate == date && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        var memberIds = reservations
            .SelectMany(r => r.PlayerMemberAccountIds.Append(r.BookingMemberAccountId))
            .Distinct()
            .ToList();

        var members = await _dbContext.MemberAccounts
            .AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberAccountId))
            .Select(m => new MemberInfo(m.MemberAccountId, m.FirstName, m.LastName))
            .ToDictionaryAsync(m => m.MemberAccountId, cancellationToken);

        return await BuildSlotsForDateAsync(date, reservations, members, memberCategory, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<BookedSlotWithMembers>>> GetBookedSlotsWithMembersForRangeAsync(
        IReadOnlyList<DateOnly> dates,
        MembershipCategory? memberCategory = null,
        CancellationToken cancellationToken = default)
    {
        if (dates.Count == 0)
            return new Dictionary<DateOnly, IReadOnlyList<BookedSlotWithMembers>>();

        var from = dates.Min();
        var to = dates.Max();

        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.SlotDate >= from && r.SlotDate <= to && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        var memberIds = reservations
            .SelectMany(r => r.PlayerMemberAccountIds.Append(r.BookingMemberAccountId))
            .Distinct()
            .ToList();

        var members = await _dbContext.MemberAccounts
            .AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberAccountId))
            .Select(m => new MemberInfo(m.MemberAccountId, m.FirstName, m.LastName))
            .ToDictionaryAsync(m => m.MemberAccountId, cancellationToken);

        var byDate = reservations.ToLookup(r => r.SlotDate);

        var result = new Dictionary<DateOnly, IReadOnlyList<BookedSlotWithMembers>>();
        foreach (var date in dates)
            result[date] = await BuildSlotsForDateAsync(date, byDate[date], members, memberCategory, cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<BookedSlotWithMembers>> BuildSlotsForDateAsync(
        DateOnly date,
        IEnumerable<Reservation> reservations,
        Dictionary<Guid, MemberInfo> members,
        MembershipCategory? memberCategory,
        CancellationToken cancellationToken)
    {
        var times = _scheduleTimeService.GetScheduleTimes(date);
        var grouped = reservations
            .GroupBy(r => r.SlotTime)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<BookedSlotWithMembers>();
        foreach (var time in times)
        {
            grouped.TryGetValue(time, out var slotReservations);
            var playerCount = slotReservations?.Sum(r => r.PlayerMemberAccountIds.Count + 1) ?? 0;
            var remaining = Math.Max(0, MaxCapacity - playerCount);

            var userCanBook = memberCategory is null || await EvaluateRulesAsync(
                new TeeTimeSlot(date, time, Guid.Empty, []),
                new BookingEvaluationContext(memberCategory, PrecomputedOccupancy: 0),
                cancellationToken) >= 0;

            var reservationsWithMembers = slotReservations?.Select(r =>
            {
                var bookingMember = members.TryGetValue(r.BookingMemberAccountId, out var bm)
                    ? bm : new MemberInfo(r.BookingMemberAccountId, "Unknown", "Member");
                var players = r.PlayerMemberAccountIds
                    .Select(id => members.TryGetValue(id, out var pm) ? pm : new MemberInfo(id, "Unknown", "Member"))
                    .ToList();
                return new ReservationWithMembers(r.ReservationId, bookingMember, players);
            }).ToList() ?? [];

            result.Add(new BookedSlotWithMembers(time, remaining, userCanBook, reservationsWithMembers));
        }
        return result;
    }

    public async Task<IReadOnlyList<Reservation>> GetMemberReservationsAsync(
        Guid memberAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => !r.IsCancelled
                     && (r.BookingMemberAccountId == memberAccountId
                         || r.PlayerMemberAccountIds.Contains(memberAccountId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateReservationAsync(
        TeeTimeSlot slot,
        CancellationToken cancellationToken = default)
    {
        var memberCategory = await FetchMemberCategoryAsync(slot.BookingMemberAccountId, cancellationToken);

        if (memberCategory is null)
            return -1;

        var strategy = _dbContext.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

            var context = new BookingEvaluationContext(memberCategory);
            var remaining = await EvaluateRulesAsync(slot, context, cancellationToken);

            if (remaining < 0)
                return -1;

            var reservation = new Reservation
            {
                SlotDate = slot.SlotDate,
                SlotTime = slot.SlotTime,
                BookingMemberAccountId = slot.BookingMemberAccountId,
                PlayerMemberAccountIds = slot.PlayerMemberAccountIds
            };

            _dbContext.Reservations.Add(reservation);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _availabilityUpdates.Notify(slot.SlotDate);
            return remaining;
        });
    }

    public async Task<int> UpdateReservationAsync(
        Guid reservationId,
        List<Guid> playerMemberAccountIds,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId && !r.IsCancelled, cancellationToken);

        if (reservation is null)
            return -1;

        var memberCategory = await FetchMemberCategoryAsync(reservation.BookingMemberAccountId, cancellationToken);

        if (memberCategory is null)
            return -1;

        var strategy = _dbContext.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

            var slot = new TeeTimeSlot(
                reservation.SlotDate,
                reservation.SlotTime,
                reservation.BookingMemberAccountId,
                playerMemberAccountIds);

            var context = new BookingEvaluationContext(memberCategory, ExcludeReservationId: reservationId);
            var remaining = await EvaluateRulesAsync(slot, context, cancellationToken);

            if (remaining < 0)
                return -1;

            reservation.PlayerMemberAccountIds = playerMemberAccountIds;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _availabilityUpdates.Notify(reservation.SlotDate);
            return remaining;
        });
    }

    public async Task<bool> CancelReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId && !r.IsCancelled, cancellationToken);

        if (reservation is null)
            return false;

        var slotDate = reservation.SlotDate;
        reservation.IsCancelled = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _availabilityUpdates.Notify(slotDate);
        return true;
    }

    private async Task<int> EvaluateRulesAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken)
    {
        var min = int.MaxValue;

        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(slot, context, cancellationToken);

            if (result < 0)
                return -1;

            min = Math.Min(min, result);
        }

        return min == int.MaxValue ? MaxCapacity : Math.Min(min, MaxCapacity);
    }

    private Task<MembershipCategory?> FetchMemberCategoryAsync(Guid memberAccountId, CancellationToken cancellationToken) =>
        _dbContext.MemberAccounts
            .Where(m => m.MemberAccountId == memberAccountId)
            .Select(m => (MembershipCategory?)m.MembershipCategory)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed record SlotAvailability(TimeOnly Time, int RemainingCapacity);
public sealed record DayAvailability(DateOnly Date, IReadOnlyList<SlotAvailability> Slots);
public sealed record BookedSlot(TimeOnly Time, int RemainingCapacity, IReadOnlyList<Reservation> Reservations);
public sealed record MemberInfo(Guid MemberAccountId, string FirstName, string LastName);
public sealed record ReservationWithMembers(Guid ReservationId, MemberInfo BookingMember, IReadOnlyList<MemberInfo> Players);
public sealed record BookedSlotWithMembers(TimeOnly Time, int RemainingCapacity, bool UserCanBook, IReadOnlyList<ReservationWithMembers> Reservations);
