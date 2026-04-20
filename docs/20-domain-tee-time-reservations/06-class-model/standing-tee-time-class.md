# StandingTeeTime (Domain Class)

## Purpose
Represents a recurring tee-time request submitted by a Shareholder member for a fixed foursome slot on a specific day of the week across a date range. Once approved by staff, it feeds into weekly tee-sheet generation.

## Responsibilities
- Store all member-supplied request fields (day, time, tolerance, date range, player list).
- Track the staff-assigned priority number and approved tee time.
- Track the request lifecycle status from Draft through terminal states.
- Link the booking member (Shareholder) and exactly 3 additional participants.
- Expose computed participant views without database persistence overhead.
- Serve as the parent record for any `TeeTimeBooking` records generated from this standing request.

## Core Properties

| Property | Type | Required | Notes |
|---|---|---|---|
| `Id` | `int` | Yes | Identity PK, auto-generated |
| `BookingMemberId` | `int` | Yes | FK to `MemberShipInfo`; the requesting Shareholder |
| `BookingMember` | `MemberShipInfo` | Yes | Navigation property |
| `RequestedDayOfWeek` | `DayOfWeek` | Yes | The recurring day of week for the slot |
| `RequestedTime` | `TimeOnly` | Yes | The member's preferred tee time |
| `ToleranceMinutes` | `int` | Yes | Acceptable time window ±minutes; `[Range(0, 120)]`, default `30` |
| `StartDate` | `DateOnly` | Yes | First date the standing slot should be active; `init` only |
| `EndDate` | `DateOnly` | Yes | Last date the standing slot should be active; `init` only |
| `PriorityNumber` | `int?` | No | Staff-assigned priority for conflict resolution; nullable until approved |
| `ApprovedTime` | `TimeOnly?` | No | Staff-confirmed tee time (may differ from `RequestedTime`); nullable until approved |
| `Status` | `StandingTeeTimeStatus` | Yes | Lifecycle state; default `Draft` |
| `AdditionalParticipants` | `List<MemberShipInfo>` | Yes | The 3 other players in the foursome (not the booking member) |

## Computed / NotMapped Properties

| Property | Type | Description |
|---|---|---|
| `ParticipantCount` | `int` | `1 + AdditionalParticipants.Count` — total players including booking member |
| `Participants` | `IReadOnlyList<MemberShipInfo>` | Booking member first, then additional participants |
| `GeneratedBookings` | `List<TeeTimeBooking>` | Lazy field-backed list; tracks `TeeTimeBooking` records created from this request (not persisted via navigation, populated manually when needed) |

## Invariants (rules that must always be true)

- `BookingMemberId` is required.
- A valid submission requires exactly 3 `AdditionalParticipants` (foursome = 4 total).
- `EndDate` must be strictly after `StartDate`.
- No duplicate members in `AdditionalParticipants`.
- `BookingMember` must not appear in `AdditionalParticipants`.
- Initial status is always `Draft`.

## Allowed Status Transitions

| From | To | Actor |
|---|---|---|
| `Draft` | `Approved` | Admin/Clerk |
| `Draft` | `Denied` | Admin/Clerk |
| `Approved` | `Allocated` | System (allocation logic — deferred) |
| `Approved` | `Unallocated` | System (when allocation fails — deferred) |
| `Draft` | `Cancelled` | Booking Member or Admin |
| `Approved` | `Cancelled` | Booking Member or Admin |
| `Allocated` | `Cancelled` | Booking Member or Admin |
| `Unallocated` | `Cancelled` | Booking Member or Admin |
| `Denied` | *(terminal)* | — |
| `Cancelled` | *(terminal)* | — |

## Explicit Non-Rules

- `StandingTeeTime` does **not** store `ApprovedBy` (the staff member who approved) or `ApprovedDate` (the timestamp of the approval decision). These are listed as shaded staff fields in `BusinessProblem.md` but are **not implemented** — see the Requirements Gap section below.
- This entity does not enforce that `BookingMember` holds a Shareholder membership level. That constraint is enforced at the authorization layer via the `BookStandingTeeTime` permission claim. The entity and service accept any `MemberShipInfo`.
- `GeneratedBookings` is `[NotMapped]` and is not loaded automatically by EF Core. It must be populated explicitly when needed.

## Requirements Gap

The following fields from the business problem's standing tee time request card are **not yet stored** in this entity:

| Business Field | Status | Notes |
|---|---|---|
| `Approved By` | Missing — deferred | Staff member name/ID who performed the approval |
| `Approved Date` | Missing — deferred | Date/time the approval decision was recorded |

These fields exist in the physical standing tee time request card described in `BusinessProblem.md` (shaded staff area). They are candidates for a future update to `StandingTeeTime` and `StandingTeeTimeService.ApproveAsync`.

## C# Class Definition

```csharp
[Index(nameof(BookingMemberId))]
[Index(nameof(RequestedDayOfWeek), nameof(RequestedTime))]
public class StandingTeeTime
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    [ForeignKey(nameof(BookingMember))]
    public int BookingMemberId { get; init; }

    [Required]
    public required MemberShipInfo BookingMember { get; init; }

    [Required]
    public DayOfWeek RequestedDayOfWeek { get; set; }

    [Required]
    public TimeOnly RequestedTime { get; set; }

    [Range(0, 120)]
    public int ToleranceMinutes { get; set; } = 30;

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    public int? PriorityNumber { get; set; }

    public TimeOnly? ApprovedTime { get; set; }

    [Required]
    public StandingTeeTimeStatus Status { get; set; } = StandingTeeTimeStatus.Draft;

    public List<MemberShipInfo> AdditionalParticipants { get; set; } = new(3);

    [NotMapped]
    public int ParticipantCount => 1 + AdditionalParticipants.Count;

    [NotMapped]
    public IReadOnlyList<MemberShipInfo> Participants => [BookingMember, .. AdditionalParticipants];

    [NotMapped]
    public List<TeeTimeBooking> GeneratedBookings => field ??= new();
}

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
