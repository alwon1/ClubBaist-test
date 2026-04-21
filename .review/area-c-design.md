# Area C: Standing Tee Times — Design Analysis

---

## Summary

The `StandingTeeTime` entity is shaped for a single lifecycle event — submit, approve, allocate once — but the business process is a *recurring weekly workflow* that spans months. Every structural gap identified in the gap analysis (unreachable statuses, missing audit fields, null navigation properties, no allocation engine) is a direct consequence of this mismatch. The entity needs a companion table to hold one row per calendar week per request; only then can statuses like Allocated/Unallocated become meaningful. The service layer needs to shed allocation responsibility into a dedicated engine, and several mechanical defects (AsNoTracking without Include, the 08:00 default, the ±60 min tolerance option) are straightforward to fix once the data model is settled.

---

## Entity Design Issues (StandingTeeTime)

### Single-row-per-request is wrong for weekly recurrence

`StandingTeeTime` holds one `Status` value for the entire `StartDate`–`EndDate` range. That works if a standing tee time is a one-shot event, but the business treats it as a season-long recurring slot. A request covering April–October must be allocated (or fail to allocate) independently for each of ~26 weeks. There is no row, field, or collection on the entity that could represent "allocated on week 3, unallocated on week 7 due to a members' day tournament, allocated again on week 8."

The entity-level `Status` can only answer "has this request ever been allocated" or "is it active." It cannot answer "what happened this week," which is what clerks and members both need to see.

### GeneratedBookings is [NotMapped]

The `GeneratedBookings` property is decorated `[NotMapped]` and is initialised with a C# 13 `field`-backed semi-auto property (`field ??= new()`). This means it is never populated from the database. Any code that traverses `request.GeneratedBookings` will always see an empty list unless EF global lazy loading is configured — and if it is, the property still cannot be lazy-loaded because it is not virtual and is not mapped to a navigation. This property is structurally dead. The actual FK (`StandingTeeTimeId` on `TeeTimeBooking`) lives on the other side of the relationship, so EF would need an explicit `.Include(s => s.GeneratedBookings)` call to populate it, but there is nothing for EF to Include because the property is not mapped.

### AdditionalParticipants is a many-to-many to MemberShipInfo

`AdditionalParticipants` is a `List<MemberShipInfo>` navigation property. EF will create a join table for this automatically. That is fine, but because `GetAllAsync` and `GetForMemberAsync` issue no `.Include(s => s.AdditionalParticipants)` and no `.Include(s => s.BookingMember)`, both collections arrive null/empty in every read path that goes through the service layer. The admin grid and member history page will throw a `NullReferenceException` on `context.BookingMember.User.FirstName` at runtime unless the app has global lazy loading enabled (not visible in the reviewed files and not a safe assumption).

### ApprovedBy and ApprovedDate are absent

The entity has `ApprovedTime` (the clock time assigned to the tee slot) but no `ApprovedBy` (the staff identity who clicked Confirm) and no `ApprovedDate` (the timestamp at which approval was recorded). The `ApproveAsync` method receives no actor parameter and persists nothing about who approved or when. These are basic audit fields that the business card requires.

### ToleranceMinutes range allows 120 minutes

The `[Range(0, 120)]` annotation permits up to ±120 minutes of tolerance. The business spec caps this at ±30 minutes. The annotation should be `[Range(0, 30)]`, and the `±60 min` option in `StandingRequest.razor` should be removed. The domain is also the wrong place to enforce a business policy if that policy could change per membership level — a named constant (`MaxToleranceMinutes = 30`) is preferable to a hard-coded annotation literal.

### PriorityNumber is not unique

`PriorityNumber` is a nullable `int` with no uniqueness constraint. If two requests receive the same priority, the allocation order is undefined. The column should carry a unique filtered index (filtering out NULLs) or allocation should break ties by a secondary criterion (e.g., submission timestamp, which is also not stored).

---

## Status / Recurrence Model Analysis

### The current enum cannot model per-week outcomes

```csharp
public enum StandingTeeTimeStatus
{
    Draft = 0,
    Approved = 1,
    Allocated = 2,
    Unallocated = 3,
    Cancelled = 4,
    Denied = 5
}
```

`Allocated` and `Unallocated` make sense as *weekly outcomes*, not as *lifetime states*. Once a request is Approved and the first week runs, it should be both Allocated (week 1) and potentially Unallocated (week 3) simultaneously. A single enum field on the parent request cannot represent this. The statuses are not wrong per se — they are right for a per-week child table — but they are incorrectly placed on the parent entity.

The parent entity needs only: `Draft → Approved → Active → Cancelled / Denied / Expired`. Whether any given week was filled is tracked by the child table.

### No weekly recurrence trigger exists

There is no scheduled job, no hosted service, no Hangfire/Quartz registration, and no admin UI action that would trigger "run this week's allocation." The allocation must happen once per week per active approved request, timed so that the tee sheet is ready at least one week in advance. This is an operational workflow gap, not just a data model gap.

### The one-active-request check is too blunt

`SubmitRequestAsync` rejects any second submission while a non-Cancelled/non-Denied request exists. This means a member cannot submit a new request for the following season while their current-season request is still active. The correct constraint is probably: one request per member per `(RequestedDayOfWeek, overlapping date range)`, not one request per member globally.

---

## Service Design Issues

### AsNoTracking with no Include — systemic, not one-off

Both read methods (`GetAllAsync`, `GetForMemberAsync`) use `AsNoTracking()` without any `.Include()`. This is a systemic pattern that will cause null navigation property access across every consuming page:

- `context.BookingMember.User.FirstName` — NullReferenceException
- `context.AdditionalParticipants` — returns empty list, silently hiding players

The fix for the read-only display paths is to add the necessary Includes:

```csharp
db.StandingTeeTimes
    .AsNoTracking()
    .Include(s => s.BookingMember).ThenInclude(m => m.User)
    .Include(s => s.BookingMember).ThenInclude(m => m.MembershipLevel)
    .Include(s => s.AdditionalParticipants).ThenInclude(p => p.User)
```

`AsNoTracking` is correct for read-only projections but must always be paired with the right Includes. The `FindAsync` calls in `ApproveAsync`, `DenyAsync`, and `CancelAsync` do not need Includes because they only update scalar fields — those paths are fine.

### StandingTeeTimeService is doing too much

The service currently handles: request submission validation, approval, denial, cancellation, and will eventually need to own allocation, weekly recurrence triggering, and special-event conflict resolution. That is at least three distinct concerns:

1. **Request lifecycle** (submit, approve, deny, cancel) — current service is the right home.
2. **Allocation engine** (weekly run, priority ordering, slot finding, conflict checking) — should be a separate `StandingAllocationService` or `StandingAllocationEngine`. It will depend on the tee-sheet/availability system, which the request lifecycle service should not know about.
3. **Recurrence scheduling** (triggering allocation each week) — belongs in a background hosted service or a Hangfire-scheduled job, not in a transactional service class.

Mixing these into one class will make `StandingTeeTimeService` very large and untestable because allocation depends on external state (available tee slots, special events calendar) that is hard to stub alongside the simpler CRUD logic.

### Shareholder check absent from service layer

`SubmitRequestAsync` does not verify that the booking member holds a Shareholder membership level. The Razor page loads `currentMember.MembershipLevel` and displays it, but never checks it before calling the service. The service, which is the trust boundary, also skips the check. A non-shareholder who somehow obtains the `BookStandingTeeTime` claim (e.g., via a misconfigured role grant) can submit successfully.

### ApproveAsync accepts no actor identity

The signature is `ApproveAsync(int id, TimeOnly approvedTime, int? priorityNumber)`. There is nowhere to pass who is performing the approval. Once `ApprovedBy` and `ApprovedDate` are added to the entity, this signature must become `ApproveAsync(int id, TimeOnly approvedTime, int? priorityNumber, string approvedByUserId)` (or equivalent identity token).

---

## Recommended Data Model Changes

### Introduce StandingTeeTimeWeekAllocation

The simplest model that satisfies all stated requirements — one request, weekly recurrence, per-week status tracking, priority ordering — adds one child table:

```csharp
public class StandingTeeTimeWeekAllocation
{
    [Key]
    public int Id { get; init; }

    [Required]
    [ForeignKey(nameof(StandingTeeTime))]
    public int StandingTeeTimeId { get; init; }

    public StandingTeeTime StandingTeeTime { get; init; } = null!;

    // The Monday of the week this allocation covers
    [Required]
    public DateOnly WeekStartDate { get; init; }

    [Required]
    public WeekAllocationStatus Status { get; set; }

    // FK to the booking that was created, null if Unallocated
    public int? TeeTimeBookingId { get; set; }
    public TeeTimeBooking? TeeTimeBooking { get; set; }

    public string? UnallocatedReason { get; set; } // e.g. "Special event: Member's Day"
}

public enum WeekAllocationStatus
{
    Pending = 0,     // week not yet processed
    Allocated = 1,   // booking created
    Unallocated = 2, // no slot available
    Skipped = 3,     // special event, no attempt made
    Cancelled = 4,   // parent request was cancelled mid-season
}
```

`StandingTeeTime` adds:

```csharp
public List<StandingTeeTimeWeekAllocation> WeekAllocations { get; set; } = [];
```

The parent entity status enum simplifies to:

```csharp
public enum StandingTeeTimeStatus
{
    Draft = 0,
    Approved = 1,
    Active = 2,      // at least one week has been processed
    Cancelled = 3,
    Denied = 4,
    Expired = 5,     // EndDate passed, all weeks complete
}
```

`Allocated` and `Unallocated` move off the parent enum entirely and become values of `WeekAllocationStatus`.

### Add audit fields to StandingTeeTime

```csharp
public string? ApprovedByUserId { get; set; }      // FK or string identity
public DateTime? ApprovedDateUtc { get; set; }
```

These are nullable because they are not populated until approval. No migration complexity beyond adding two nullable columns.

### Fix ToleranceMinutes range

Change `[Range(0, 120)]` to `[Range(0, 30)]` and remove the `±60 min` option from `StandingRequest.razor`. Optionally extract `MaxToleranceMinutes = 30` to a constants class so both the entity and the UI reference the same value.

### Add unique filtered index on PriorityNumber

```csharp
[Index(nameof(PriorityNumber), IsUnique = true, Filter = "\"PriorityNumber\" IS NOT NULL")]
```

or enforce uniqueness in `ApproveAsync` by querying for conflicts before assigning the priority number.

### Fix GeneratedBookings

Remove the `[NotMapped] GeneratedBookings` property and replace it with the properly mapped `WeekAllocations` navigation property above. If a flat list of bookings is still needed for display, project it from `WeekAllocations.Where(w => w.TeeTimeBookingId != null).Select(w => w.TeeTimeBooking)`.

### Priority ordering — secondary sort key

Add `CreatedAtUtc` (a `DateTime` set on insert) so that when two requests share the same `PriorityNumber`, tie-breaking is deterministic (earlier submission wins). This requires no enum change.

---

## Notes

- The `[NotMapped] GeneratedBookings` field uses the C# 13 `field` keyword semi-auto property syntax (`field ??= new()`). This is only supported on .NET 9+ SDK with `<LangVersion>preview</LangVersion>` or `13`. If the build environment is on an older SDK this will fail to compile; worth confirming the project's `<LangVersion>` setting.

- The separation of `StandingAllocationService` from `StandingTeeTimeService` should be treated as a prerequisite for implementing the allocation engine, not an optional refactor. The allocation engine will need to call into availability, special-events, and booking-creation subsystems. Pulling that logic into the existing service will make both the CRUD and allocation paths untestable in isolation.

- The admin panel `approvedTimeStr = "08:00"` default in `OpenApprovePanel` is a one-line fix: replace the hardcoded string with `request.RequestedTime.ToString("HH:mm")` using the selected row's data. This should be done as part of any approval-UI pass regardless of the larger model changes.

- The `BookStandingTeeTime` permission policy is well-placed as the outer gate, but the Shareholder membership level check must also live in `SubmitRequestAsync`. The service is the only layer that can be called from multiple entry points (Razor page, API, tests, future admin override) and is therefore the correct trust boundary for business-rule enforcement.

- `StandingTeeTimeWeekAllocation` rows should be generated eagerly when an admin runs the weekly allocation, not lazily on demand. The allocation engine should create `Pending` rows for all weeks in `StartDate`–`EndDate` when a request is first approved (or when the engine runs for the first time), and then update each row's status as the weekly slot search runs. This makes it easy to display "upcoming weeks — not yet processed" to the member without requiring the allocation engine to have run.
