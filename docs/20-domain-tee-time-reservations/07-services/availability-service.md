# TeeTimeBookingService – Availability Methods

## Responsibility
`TeeTimeBookingService` provides tee-time availability views by combining the schedule (from `IScheduleTimeService`) with current reservation occupancy, running each slot through all `IBookingRule` implementations.

Availability and reservation operations are unified in one service rather than separate services.

## Public Methods (Availability)

### GetAvailabilityAsync
```csharp
Task<IReadOnlyList<DayAvailability>> GetAvailabilityAsync(
    DateOnly from,
    DateOnly to,
    CancellationToken cancellationToken = default)
```

**Inputs:** start and end date (inclusive). Works for a single day (`from == to`) or a multi-day range (e.g., weekly planner view).

**Output:**
```
DayAvailability
  Date: DateOnly
  Slots: IReadOnlyList<SlotAvailability>
    Time: TimeOnly
    RemainingCapacity: int   // 0 = full, positive = spots left
```

**Behaviour:**
- Fetches all active reservations for the entire range in a **single batch query**.
- Computes occupancy per (date, time) in memory.
- Calls `IScheduleTimeService.GetScheduleTimes(date)` for each date.
- Runs each slot through all rules with `BookingEvaluationContext(MemberCategory: null)` — no member-specific filtering.
- `BookingWindowRule` uses `ISeasonService` to check if each date is within a season.
- `MembershipTimeRestrictionRule` is skipped (passes through) when `MemberCategory` is null.

### GetBookedTimesAsync
```csharp
Task<IReadOnlyList<BookedSlot>> GetBookedTimesAsync(
    DateOnly date,
    CancellationToken cancellationToken = default)
```

**Output:**
```
BookedSlot
  Time: TimeOnly
  RemainingCapacity: int   // clamped to [0, 4]
  Reservations: IReadOnlyList<Reservation>  // active reservations for this slot
```

Returns schedule times with reservation details attached, for building a booked-times view (who is booked, remaining spots).

## Schedule Time Service

```csharp
public interface IScheduleTimeService
{
    IReadOnlyList<TimeOnly> GetScheduleTimes(DateOnly date);
}
```

`DefaultScheduleTimeService` generates times from 7:00 AM to 7:00 PM using alternating 7/8-minute gaps (7:00, 7:07, 7:15, 7:22, 7:30 ...) to achieve a 7.5-minute average interval. Swap the registration to use a different implementation.

## Core Rules

- `RemainingCapacity` is clamped to `[0, MaxCapacity]` where `MaxCapacity = 4`.
- Availability queries use `Guid.Empty` as the booking member; `SlotCapacityRule` requests 0 players for these.
- Dates outside any Active or Planned season return `RemainingCapacity = 0` for all slots (via `BookingWindowRule`).
- Date range is unbounded — callers choose the range size. The single-query batch fetch keeps this efficient.
