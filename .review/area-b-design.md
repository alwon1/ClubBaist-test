# Area B: Regular Tee Times — Design Analysis

## Summary

The core booking pipeline is structurally sound: `TeeTimeSlot` and `TeeTimeBooking` model the slot/booking relationship cleanly, `TeeTimeEvaluation` provides a composable projection shape, and the `IBookingRule` pipeline translates naturally to EF-pushable LINQ. Three categories of work are needed before the feature is production-ready: a primary-key redesign on `TeeTimeSlot`, several missing fields on `TeeTimeBooking`, and the addition of a 7-day advance booking rule. The off-by-one boundary in `MembershipLevelAvailabilityRule` also needs a one-character fix.

---

## TeeTimeSlot PK Design Issue

**Current state.** `TeeTimeSlot.Start` (a `DateTime`) is declared as `[Key]` and simultaneously carries a duplicate `[Index]` attribute, which is redundant — the primary key is already indexed.

**Problems with a DateTime PK.**

1. *Precision collisions.* Two slots generated with millisecond-level `DateTime` values that differ only in sub-tick precision will be treated as different rows even though they represent the same tee time. Conversely, any rounding difference across system boundaries (e.g. serialisation through JSON) can silently create a second row instead of matching the existing one.
2. *FK brittleness.* `TeeTimeBooking.TeeTimeSlotStart` is a `DateTime` FK. Any clock or serialisation discrepancy between layers will produce a broken reference that is difficult to diagnose.
3. *Mutability hazard.* `Start` is `init`-only, so it cannot be changed in place. If a slot needs to be rescheduled the row must be deleted and recreated, which cascades to all child `TeeTimeBooking` rows.
4. *EF clustered index cost.* Using a `DateTime` as a clustered PK inserts rows out-of-order whenever slots are not created in chronological order, causing page splits.

**Recommended approach.**

Introduce a surrogate integer PK and demote `Start` to a unique constraint:

```csharp
[Key]
[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
public int Id { get; init; }

[Required]
[Index(IsUnique = true)]          // enforce natural uniqueness
public DateTime Start { get; init; }
```

`TeeTimeBooking.TeeTimeSlotStart` becomes `TeeTimeBooking.TeeTimeSlotId` (`int`), pointing at the new surrogate key. The duplicate `[Index(nameof(Start))]` on `TeeTimeSlot` is then unnecessary and should be removed; the unique constraint implies an index.

---

## TeeTimeBooking Missing Fields

**Fields present but not needed by spec.** None are obviously gratuitous; `StandingTeeTimeId` links to the Standing Tee Times feature and is intentionally nullable.

**Fields missing for the daily tee sheet.**

| Field | Type | Notes |
|---|---|---|
| `NumberOfCarts` | `int` | Count of carts requested for this booking group. Range 0–2 is typical; no upper bound is imposed by the domain so validation belongs in a rule or the application layer. |
| `EmployeeName` | `string?` | Name of the staff member who made the booking on behalf of a member (walk-up / phone booking). Nullable — absent for member self-service bookings. |

These two fields are pure data properties with no rule implications; they do not need to participate in `TeeTimeEvaluation`.

**Minor observation.** `AdditionalParticipants` is initialised with `new(3)` (capacity hint), which is fine, but the field is `List<MemberShipInfo>` — a full navigation collection. If EF owns this collection the initialiser capacity hint is silently overwritten on load; it does no harm but is mildly misleading.

---

## Booking Rule Pattern Analysis

**Interface shape.** `IBookingRule` defines three `Evaluate` overloads:

- `Evaluate(query, TeeTimeBooking, int? excludeBookingId)` — used during booking attempts.
- `Evaluate(query, MembershipLevel)` — used for availability display by tier.
- `Evaluate(query, MemberShipInfo)` — used for per-member availability display.

Each overload receives an `IQueryable<TeeTimeEvaluation>` and returns a new one, enabling a pipeline (`reduce`-style) that EF can translate to a single composed SQL query. This is clean and composable. The convention of "first writer wins" (rules check `SpotsRemaining < 0` before overwriting) is consistently applied across all five existing rules, which prevents later rules from accidentally un-rejecting a slot.

**What a 7-day advance booking rule looks like.**

The rule is structurally identical to `PastSlotRule` — it is a pure time-window check with no injected dependencies:

```csharp
/// <summary>
/// Prevents members from booking more than 7 days in advance.
/// </summary>
public class AdvanceBookingWindowRule(int maxDaysAhead = 7) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(
        IQueryable<TeeTimeEvaluation> query,
        TeeTimeBooking booking,
        int? excludeBookingId = null)
    {
        var cutoff = DateTime.SpecifyKind(DateTime.Now.AddDays(maxDaysAhead), DateTimeKind.Unspecified);

        return query.Select(p => new TeeTimeEvaluation(
            p.Slot,
            p.SpotsRemaining < 0
                ? p.SpotsRemaining
                : p.Slot.Start > cutoff
                    ? -6
                    : p.SpotsRemaining,
            p.SpotsRemaining < 0
                ? p.RejectionReason
                : p.Slot.Start > cutoff
                    ? $"Tee times can only be booked up to {maxDaysAhead} days in advance"
                    : p.RejectionReason));
    }
}
```

The sentinel value `-6` continues the existing convention (`-1` = membership level, `-2` = duplicate, `-3` = special event, `-5` = past). No `Evaluate(MembershipLevel)` / `Evaluate(MemberShipInfo)` overloads are needed unless the availability display should also suppress out-of-window slots (recommended — otherwise members see bookable-looking slots they cannot actually book).

**Holiday detection.** There is currently no `HolidayBlockingRule`. A holiday is semantically similar to a `SpecialEvent`, so the simplest approach is to treat public holidays as a special event category (add a `IsHoliday` flag to `SpecialEvent`) and let `SpecialEventBlockingRule` handle them, or define a dedicated `HolidayBlockingRule` injected with an `IQueryable<Holiday>`. The latter is cleaner for reporting but either works within the existing pipeline.

---

## Boundary Bug Fix Recommendation

**Affected rule.** `MembershipLevelAvailabilityRule`, line 19:

```csharp
a.EndTime >= TimeOnly.FromDateTime(p.Slot.Start)   // BUG: inclusive upper bound
```

**The problem.** If a membership tier's availability window ends at, say, 12:00, a slot *starting* at exactly 12:00 is currently considered available because `>=` passes. A slot that starts at the boundary of a window is conventionally the first slot *outside* that window (half-open interval semantics: `[StartTime, EndTime)`).

**Fix.** Change the upper bound to exclusive:

```csharp
a.EndTime > TimeOnly.FromDateTime(p.Slot.Start)
```

This makes the window `[StartTime, EndTime)`, consistent with how `SpecialEventBlockingRule` models its event range (`e.Start <= slot && e.End > slot` — already correct). Both rules should use the same half-open convention.

The lower bound (`a.StartTime <= ...`) is correctly inclusive and should not change.

---

## Recommended Data Model Changes

**TeeTimeSlot**

- Replace `DateTime Start` PK with `int Id` surrogate PK.
- Retain `Start` as a `[Required]` unique-constrained column.
- Remove the now-redundant `[Index(nameof(Start))]` attribute (unique constraint implies index).

**TeeTimeBooking**

- Change FK column `DateTime TeeTimeSlotStart` to `int TeeTimeSlotId` to reference the new surrogate PK.
- Add `int NumberOfCarts { get; set; }` (default 0).
- Add `string? EmployeeName { get; set; }` (null = self-service booking).

**New rule**

- Add `AdvanceBookingWindowRule` (7-day window, configurable) to the booking pipeline.

**Optional / lower priority**

- Add `HolidayBlockingRule` or extend `SpecialEvent` with an `IsHoliday` flag.
- Consider exposing `NumberOfCarts` on `TeeTimeEvaluation` as a derived total per slot for the tee-sheet view (sum of all bookings' carts for that slot).

---

## Notes

- `TeeTimeEvaluation` is a `record struct` with three fields (Slot, SpotsRemaining, RejectionReason). This is an appropriate shape: it is immutable, value-typed, and carries exactly the information each rule needs to read and write. No changes are recommended to the record itself.
- The `SpotsRemaining < 0` sentinel pattern works but conflates "rejected" with "negative count". A dedicated `bool IsRejected` flag or a discriminated union would be cleaner long-term, but refactoring it is a larger change and out of scope for the current gap.
- `PastSlotRule` uses `DateTime.Now` (wall-clock, unspecified kind) which ties the rule to the server's local timezone. If the club ever operates across timezones this will need to be injected as `IClock` or `TimeProvider`.
