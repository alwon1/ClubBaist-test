using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class TeeTimeBookingService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IScheduleTimeService _scheduleTimeService;
    private readonly IReadOnlyList<IBookingRule> _rules;
    private readonly IApplicationDbContext<TKey> _dbContext;

    public TeeTimeBookingService(
        IScheduleTimeService scheduleTimeService,
        IEnumerable<IBookingRule> rules,
        IApplicationDbContext<TKey> dbContext)
    {
        _scheduleTimeService = scheduleTimeService;
        _rules = rules.ToList();
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DayAvailability>> GetAvailabilityAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var activeSeason = await FetchActiveSeasonAsync(cancellationToken);
        var context = new BookingEvaluationContext(activeSeason, MemberCategory: null);

        var days = new List<DayAvailability>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var times = _scheduleTimeService.GetScheduleTimes(date);
            var slots = new List<SlotAvailability>();

            foreach (var time in times)
            {
                var slot = new TeeTimeSlot(date, time, Guid.Empty, []);
                var remaining = await EvaluateRulesAsync(slot, context, cancellationToken);
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
            .Where(r => r.SlotDate == date && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        var grouped = reservations
            .GroupBy(r => r.SlotTime)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<BookedSlot>();

        foreach (var time in times)
        {
            grouped.TryGetValue(time, out var slotReservations);
            var playerCount = slotReservations?.Sum(r => r.PlayerMemberAccountIds.Count) ?? 0;
            var remaining = 4 - playerCount;

            result.Add(new BookedSlot(time, remaining, slotReservations ?? []));
        }

        return result;
    }

    public async Task<IReadOnlyList<Reservation>> GetMemberReservationsAsync(
        Guid memberAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .Where(r => !r.IsCancelled
                     && (r.BookingMemberAccountId == memberAccountId
                         || r.PlayerMemberAccountIds.Contains(memberAccountId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateReservationAsync(
        TeeTimeSlot slot,
        CancellationToken cancellationToken = default)
    {
        var activeSeason = await FetchActiveSeasonAsync(cancellationToken);
        var memberCategory = await FetchMemberCategoryAsync(slot.BookingMemberAccountId, cancellationToken);

        if (memberCategory is null)
            return 0;

        var context = new BookingEvaluationContext(activeSeason, memberCategory);
        var remaining = await EvaluateRulesAsync(slot, context, cancellationToken);

        if (remaining <= 0)
            return 0;

        var reservation = new Reservation
        {
            SlotDate = slot.SlotDate,
            SlotTime = slot.SlotTime,
            BookingMemberAccountId = slot.BookingMemberAccountId,
            PlayerMemberAccountIds = slot.PlayerMemberAccountIds
        };

        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return remaining;
    }

    public async Task<int> UpdateReservationAsync(
        Guid reservationId,
        List<Guid> playerMemberAccountIds,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId && !r.IsCancelled, cancellationToken);

        if (reservation is null)
            return 0;

        if (!playerMemberAccountIds.Contains(reservation.BookingMemberAccountId))
            return 0;

        var activeSeason = await FetchActiveSeasonAsync(cancellationToken);
        var memberCategory = await FetchMemberCategoryAsync(reservation.BookingMemberAccountId, cancellationToken);

        if (memberCategory is null)
            return 0;

        var slot = new TeeTimeSlot(
            reservation.SlotDate,
            reservation.SlotTime,
            reservation.BookingMemberAccountId,
            playerMemberAccountIds);

        var context = new BookingEvaluationContext(activeSeason, memberCategory);
        var remaining = await EvaluateRulesAsync(slot, context, cancellationToken);

        if (remaining <= 0)
            return 0;

        reservation.PlayerMemberAccountIds = playerMemberAccountIds;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return remaining;
    }

    public async Task<bool> CancelReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId && !r.IsCancelled, cancellationToken);

        if (reservation is null)
            return false;

        reservation.IsCancelled = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<int> EvaluateRulesAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken)
    {
        var min = int.MaxValue;

        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(slot, context, cancellationToken);

            if (result <= 0)
                return 0;

            min = Math.Min(min, result);
        }

        return min == int.MaxValue ? 4 : min;
    }

    private Task<Season?> FetchActiveSeasonAsync(CancellationToken cancellationToken) =>
        _dbContext.Seasons
            .FirstOrDefaultAsync(s => s.SeasonStatus == SeasonStatus.Active, cancellationToken);

    private Task<MembershipCategory?> FetchMemberCategoryAsync(Guid memberAccountId, CancellationToken cancellationToken) =>
        _dbContext.MemberAccounts
            .Where(m => m.MemberAccountId == memberAccountId)
            .Select(m => (MembershipCategory?)m.MembershipCategory)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed record SlotAvailability(TimeOnly Time, int RemainingCapacity);
public sealed record DayAvailability(DateOnly Date, IReadOnlyList<SlotAvailability> Slots);
public sealed record BookedSlot(TimeOnly Time, int RemainingCapacity, IReadOnlyList<Reservation> Reservations);
