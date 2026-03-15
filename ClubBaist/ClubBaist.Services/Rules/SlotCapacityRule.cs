using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

public class SlotCapacityRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private const int MaxCapacity = 4;

    private readonly IApplicationDbContext<TKey> _dbContext;

    public SlotCapacityRule(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        int occupancy;

        if (context.PrecomputedOccupancy.HasValue)
        {
            occupancy = context.PrecomputedOccupancy.Value;
        }
        else
        {
            var query = _dbContext.Reservations
                .Where(r => r.SlotDate == slot.SlotDate
                         && r.SlotTime == slot.SlotTime
                         && !r.IsCancelled);

            if (context.ExcludeReservationId.HasValue)
                query = query.Where(r => r.ReservationId != context.ExcludeReservationId.Value);

            occupancy = await query
                .SumAsync(r => r.PlayerMemberAccountIds.Count + 1, cancellationToken);
        }

        // Booking member is always player #1; PlayerMemberAccountIds are additional players.
        // For availability queries (Guid.Empty booking member), requested = 0.
        var requested = slot.BookingMemberAccountId == Guid.Empty
            ? 0
            : 1 + slot.PlayerMemberAccountIds.Count;

        return Math.Max(0, MaxCapacity - occupancy - requested);
    }
}
