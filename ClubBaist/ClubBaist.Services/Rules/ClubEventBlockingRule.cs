using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

public class ClubEventBlockingRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;

    public ClubEventBlockingRule(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var isBlocked = await _dbContext.ClubEvents
            .AnyAsync(e => e.EventDate == slot.SlotDate && e.BlocksTeeSheet, cancellationToken);

        return isBlocked ? -1 : int.MaxValue;
    }
}
