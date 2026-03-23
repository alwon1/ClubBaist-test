using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

public class ClubEventBlockingRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _db;

    // Option B: per-date lazy cache — populated on first access for a date within the
    // scoped rule instance, so each unique date causes at most one DB query.
    private readonly Dictionary<DateOnly, IReadOnlyList<ClubEvent>> _dateCache = new();

    public ClubEventBlockingRule(IApplicationDbContext<TKey> db)
    {
        _db = db;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClubEvent> events;

        // Option A: use caller-prefetched events when available (zero extra queries).
        if (context.BlockedEventsByDate is not null)
        {
            context.BlockedEventsByDate.TryGetValue(slot.SlotDate, out var prefetched);
            events = prefetched ?? [];
        }
        else
        {
            // Option B: lazy-load and cache per date within the scoped instance.
            if (!_dateCache.TryGetValue(slot.SlotDate, out var cached))
            {
                cached = await _db.ClubEvents
                    .AsNoTracking()
                    .Where(e => e.EventDate == slot.SlotDate)
                    .ToListAsync(cancellationToken);
                _dateCache[slot.SlotDate] = cached;
            }
            events = cached;
        }

        var blocked = events.Any(e =>
            slot.SlotTime >= e.StartTime &&
            slot.SlotTime <= e.EndTime);

        return blocked ? -1 : int.MaxValue;
    }
}
