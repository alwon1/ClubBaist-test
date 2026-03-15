# SeasonService / ISeasonService

## Responsibility
`SeasonService` provides in-memory season lookups used by `BookingWindowRule` to determine whether a date is bookable. It is a **singleton** loaded once at application startup — restart the application to pick up season data changes.

## Interface

```csharp
public interface ISeasonService
{
    Season? GetSeasonForDate(DateOnly date);
}
```

Returns the `Season` whose date range covers the given date, or `null` if no matching season exists.

## SeasonService Implementation

```csharp
public class SeasonService : ISeasonService
{
    // Loaded at startup: Active + Planned seasons
    public Season? GetSeasonForDate(DateOnly date) =>
        _seasons.FirstOrDefault(s => s.StartDate <= date && s.EndDate >= date);
}
```

## What Seasons Are Loaded

Both `SeasonStatus.Active` and `SeasonStatus.Planned` seasons are loaded:
- **Active** — current in-play season.
- **Planned** — upcoming season. Members can book ahead into a planned season before it officially starts.
- **Closed** seasons are excluded — no bookings into past seasons.

## DI Registration

```csharp
services.AddSingleton<ISeasonService>(provider =>
{
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext<TKey>>();
    var seasons = db.Seasons
        .Where(s => s.SeasonStatus == SeasonStatus.Active
                 || s.SeasonStatus == SeasonStatus.Planned)
        .ToList();
    return new SeasonService(seasons);
});
```

The factory resolves lazily on first use, after `EnsureCreated()` has run.

## Season Domain Model

```csharp
public class Season
{
    public Guid SeasonId { get; set; }
    public string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SeasonStatus SeasonStatus { get; set; }  // Planned | Active | Closed
}
```

## Core Validation / Business Rules

- `StartDate` must be on or before `EndDate`.
- Seasons must not have overlapping date ranges (enforced at the application level when creating seasons).
- A date maps to at most one season.
- A closed season cannot be reopened.

## Future

Season mutation operations (create, activate, close) belong in a future `SeasonManagementService`. For now seasons are managed directly via the database or seed data, and the application is restarted to reflect changes.
