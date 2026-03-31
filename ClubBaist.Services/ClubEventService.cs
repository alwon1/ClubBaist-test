using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class ClubEventService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _db;

    public ClubEventService(IApplicationDbContext<TKey> db)
    {
        _db = db;
    }

    public async Task<ClubEvent> CreateAsync(
        string name,
        DateOnly eventDate,
        TimeOnly startTime,
        TimeOnly endTime,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var clubEvent = new ClubEvent
        {
            Name = name,
            EventDate = eventDate,
            StartTime = startTime,
            EndTime = endTime,
            Description = description
        };

        _db.ClubEvents.Add(clubEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return clubEvent;
    }

    public async Task<bool> UpdateAsync(
        Guid clubEventId,
        string name,
        DateOnly eventDate,
        TimeOnly startTime,
        TimeOnly endTime,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var clubEvent = await _db.ClubEvents
            .FirstOrDefaultAsync(e => e.ClubEventId == clubEventId, cancellationToken);

        if (clubEvent is null)
            return false;

        clubEvent.Name = name;
        clubEvent.EventDate = eventDate;
        clubEvent.StartTime = startTime;
        clubEvent.EndTime = endTime;
        clubEvent.Description = description;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid clubEventId,
        CancellationToken cancellationToken = default)
    {
        var clubEvent = await _db.ClubEvents
            .FirstOrDefaultAsync(e => e.ClubEventId == clubEventId, cancellationToken);

        if (clubEvent is null)
            return false;

        _db.ClubEvents.Remove(clubEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ClubEvent>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.ClubEvents
            .AsNoTracking()
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClubEvent>> GetByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await _db.ClubEvents
            .AsNoTracking()
            .Where(e => e.EventDate >= from && e.EventDate <= to)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.StartTime)
            .ToListAsync(cancellationToken);
    }
}
