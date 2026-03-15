using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class TeeTimeBookingService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IScheduleTimeService _scheduleTimeService;
    private readonly IEnumerable<IBookingRule> _rules;
    private readonly IApplicationDbContext<TKey> _dbContext;

    public TeeTimeBookingService(
        IScheduleTimeService scheduleTimeService,
        IEnumerable<IBookingRule> rules,
        IApplicationDbContext<TKey> dbContext)
    {
        _scheduleTimeService = scheduleTimeService;
        _rules = rules;
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DayAvailability>> GetAvailabilityAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var days = new List<DayAvailability>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var times = _scheduleTimeService.GetScheduleTimes(date);
            var slots = new List<SlotAvailability>();

            foreach (var time in times)
            {
                var slot = new TeeTimeSlot(date, time, Guid.Empty, []);
                var remaining = await EvaluateRulesAsync(slot, cancellationToken);
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
        var remaining = await EvaluateRulesAsync(slot, cancellationToken);

        if (remaining <= 0)
            return 0;

        var reservation = new Reservation
        {
            SlotDate = slot.SlotDate,
            SlotTime = slot.SlotTime,
            BookingMemberAccountId = slot.BookingMemberAccountId,
            PlayerMemberAccountIds = slot.PlayerMemberAccountIds.ToList()
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

        var slot = new TeeTimeSlot(
            reservation.SlotDate,
            reservation.SlotTime,
            reservation.BookingMemberAccountId,
            playerMemberAccountIds);

        var remaining = await EvaluateRulesAsync(slot, cancellationToken);

        if (remaining <= 0)
            return 0;

        reservation.PlayerMemberAccountIds = playerMemberAccountIds.ToList();
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

    private async Task<int> EvaluateRulesAsync(TeeTimeSlot slot, CancellationToken cancellationToken)
    {
        var min = int.MaxValue;

        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(slot, cancellationToken);

            if (result <= 0)
                return 0;

            min = Math.Min(min, result);
        }

        return min == int.MaxValue ? 4 : min;
    }
}

public sealed record SlotAvailability(TimeOnly Time, int RemainingCapacity);
public sealed record DayAvailability(DateOnly Date, IReadOnlyList<SlotAvailability> Slots);
public sealed record BookedSlot(TimeOnly Time, int RemainingCapacity, IReadOnlyList<Reservation> Reservations);
