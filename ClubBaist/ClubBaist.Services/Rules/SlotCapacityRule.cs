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

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, CancellationToken cancellationToken = default)
    {
        var occupancy = await _dbContext.Reservations
            .Where(r => r.SlotDate == slot.SlotDate
                     && r.SlotTime == slot.SlotTime
                     && !r.IsCancelled)
            .SumAsync(r => r.PlayerMemberAccountIds.Count, cancellationToken);

        return Math.Max(0, MaxCapacity - occupancy - slot.PlayerMemberAccountIds.Count);
    }
}
