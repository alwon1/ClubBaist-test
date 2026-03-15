# Booking Rules (IBookingRule)

## Responsibility
Booking rules evaluate whether a tee-time slot is available or a booking request is permitted. Each rule is an independent implementation of `IBookingRule`, making it easy to add, remove, or reorder policies without touching the booking service.

## Interface

```csharp
public interface IBookingRule
{
    Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default);
}
```

### Return value contract
| Value | Meaning |
|-------|---------|
| Negative | Rule denies the request |
| `0` | Slot is exactly full after this booking — accepted |
| Positive | Remaining capacity after this booking |

`TeeTimeBookingService` takes the minimum value across all rules. If any rule returns negative, the overall result is `-1` (denied). The final result is clamped to `[0, MaxCapacity]`.

## BookingEvaluationContext

Pre-fetched data passed to every rule by the booking service to avoid per-rule DB queries:

```csharp
public sealed record BookingEvaluationContext(
    MembershipCategory? MemberCategory,       // null = availability query (no member check)
    Guid? ExcludeReservationId = null,        // for update: exclude this reservation from occupancy
    int? PrecomputedOccupancy = null);        // pre-fetched for range queries; null = rule queries DB
```

## Implemented Rules

### SlotCapacityRule
- Computes slot occupancy from active `Reservation` records.
- Uses `PrecomputedOccupancy` when provided (range queries); otherwise queries DB.
- Excludes `ExcludeReservationId` from occupancy when updating an existing reservation.
- Occupancy per reservation = `1 + PlayerMemberAccountIds.Count` (booking member is always player #1).
- For availability queries (`Guid.Empty` booking member), `requested = 0`; for bookings, `requested = 1 + additional players`.
- Returns `Math.Max(0, MaxCapacity - occupancy - requested)`.

### BookingWindowRule
- Calls `ISeasonService.GetSeasonForDate(slot.SlotDate)`.
- Returns `int.MaxValue` if the date falls within an Active or Planned season; `-1` otherwise.
- No DB access — season data is held in the `SeasonService` singleton.

### MembershipTimeRestrictionRule
- Pure logic — no DB access.
- Uses `context.MemberCategory`; returns `int.MaxValue` immediately if null (availability query).
- Denies (`-1`) when the booking member's membership tier is restricted at the requested time of day / day of week:

| Tier | Members | Mon–Fri | Weekends |
|------|---------|---------|---------|
| Gold | Shareholder, Associate | Anytime | Anytime |
| Silver | ShareholderSpouse, AssociateSpouse | Before 3 PM or after 5:30 PM | After 11 AM |
| Bronze | PeeWee, Junior, Intermediate | Before 3 PM or after 6 PM | After 1 PM |
| Social | Social | Never | Never |

## Adding a New Rule
1. Implement `IBookingRule` in `ClubBaist.Services/Rules/`.
2. Register it in the DI container alongside the existing rules.
3. No changes to `TeeTimeBookingService` are required.

## TeeTimeSlot (rule input)

```csharp
public record TeeTimeSlot(
    DateOnly SlotDate,
    TimeOnly SlotTime,
    Guid BookingMemberAccountId,   // Guid.Empty for availability queries
    List<Guid> PlayerMemberAccountIds);  // non-booking additional players only
```
