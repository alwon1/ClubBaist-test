using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

public class ClubEventBlockingRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _db;

    public ClubEventBlockingRule(IApplicationDbContext<TKey> db)
    {
        _db = db;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var blocked = await _db.ClubEvents.AnyAsync(e =>
            e.EventDate == slot.SlotDate &&
            slot.SlotTime >= e.StartTime &&
            slot.SlotTime <= e.EndTime, cancellationToken);

        return blocked ? -1 : int.MaxValue;
    }
}
