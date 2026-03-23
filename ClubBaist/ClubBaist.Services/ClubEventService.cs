using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class ClubEventService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;

    public ClubEventService(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClubEvent> CreateAsync(
        string name,
        DateOnly eventDate,
        bool blocksTeeSheet,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var clubEvent = new ClubEvent
        {
            Name = name,
            EventDate = eventDate,
            BlocksTeeSheet = blocksTeeSheet,
            Description = description
        };

        _dbContext.ClubEvents.Add(clubEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return clubEvent;
    }

    public async Task<bool> UpdateAsync(
        Guid clubEventId,
        string name,
        DateOnly eventDate,
        bool blocksTeeSheet,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var clubEvent = await _dbContext.ClubEvents
            .FirstOrDefaultAsync(e => e.ClubEventId == clubEventId, cancellationToken);

        if (clubEvent is null)
            return false;

        clubEvent.Name = name;
        clubEvent.EventDate = eventDate;
        clubEvent.BlocksTeeSheet = blocksTeeSheet;
        clubEvent.Description = description;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid clubEventId,
        CancellationToken cancellationToken = default)
    {
        var clubEvent = await _dbContext.ClubEvents
            .FirstOrDefaultAsync(e => e.ClubEventId == clubEventId, cancellationToken);

        if (clubEvent is null)
            return false;

        _dbContext.ClubEvents.Remove(clubEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ClubEvent>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ClubEvents
            .AsNoTracking()
            .OrderBy(e => e.EventDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClubEvent>> GetByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ClubEvents
            .AsNoTracking()
            .Where(e => e.EventDate >= from && e.EventDate <= to)
            .OrderBy(e => e.EventDate)
            .ToListAsync(cancellationToken);
    }
}
