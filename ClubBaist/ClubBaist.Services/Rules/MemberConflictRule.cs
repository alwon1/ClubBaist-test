using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

/// <summary>
/// Prevents a member from being booked into a tee time slot they are already part of.
/// </summary>
public class MemberConflictRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;

    public MemberConflictRule(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        // Availability queries (no specific member) — nothing to check
        if (slot.BookingMemberAccountId == 0)
            return int.MaxValue;

        var allMemberIds = new HashSet<int>(slot.PlayerMemberAccountIds) { slot.BookingMemberAccountId };

        var query = _dbContext.Reservations
            .Where(r => r.SlotDate == slot.SlotDate && r.SlotTime == slot.SlotTime && !r.IsCancelled);

        if (context.ExcludeReservationId.HasValue)
            query = query.Where(r => r.ReservationId != context.ExcludeReservationId.Value);

        // Fetch to memory — slots contain at most a handful of reservations
        var existingReservations = await query.ToListAsync(cancellationToken);

        var hasConflict = existingReservations.Any(r =>
            allMemberIds.Contains(r.BookingMemberAccountId) ||
            r.PlayerMemberAccountIds.Any(pid => allMemberIds.Contains(pid)));

        return hasConflict ? -1 : int.MaxValue;
    }
}
