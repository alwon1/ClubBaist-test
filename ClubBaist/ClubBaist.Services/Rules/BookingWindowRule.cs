using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

public class BookingWindowRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;

    public BookingWindowRule(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, CancellationToken cancellationToken = default)
    {
        var hasActiveSeason = await _dbContext.Seasons
            .AnyAsync(s => s.SeasonStatus == SeasonStatus.Active
                        && s.StartDate <= slot.SlotDate
                        && s.EndDate >= slot.SlotDate,
                cancellationToken);

        return hasActiveSeason ? int.MaxValue : 0;
    }
}
