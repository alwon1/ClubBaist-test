# GolfRound (Domain Entity)

## Purpose

Represents a single completed 18-hole golf round recorded by or on behalf of a member. Links a tee time booking to the member's hole-by-hole scores. One `GolfRound` per `TeeTimeBooking` — enforced by a unique index.

## Namespace

`ClubBaist.Domain2.Entities.Scoring`

## Nested Types

`TeeColor` is declared as a nested enum inside `GolfRound`. It has no meaning outside of a golf round.

```csharp
public enum TeeColor { Red = 0, White = 1, Blue = 2 }
```

## Properties

| Property | Type | Annotation(s) | Notes |
|----------|------|---------------|-------|
| `Id` | `int` | `[Key]` `[DatabaseGenerated(Identity)]` | Auto-generated surrogate key |
| `TeeTimeBookingId` | `int` | `[Required]` `[ForeignKey(nameof(TeeTimeBooking))]` | FK to `TeeTimeBooking` — one-way navigation only |
| `TeeTimeBooking` | `TeeTimeBooking` | `[Required]` | Navigation property (no back-navigation added to `TeeTimeBooking`) |
| `BookingMemberId` | `int` | `[Required]` | Plain FK int to `MemberShipInfo` — no navigation property; avoids coupling to member domain |
| `TeeColor` | `TeeColor` | `[Required]` | Tee colour chosen for this round; future lookup key for course/slope ratings |
| `Scores` | `List<uint?>` | *(none)* | 18 nullable unsigned ints, initialized to length 18. Null = hole not yet entered. Per-hole range (1–20) validated in UI only. |
| `SubmittedAt` | `DateTime` | `[Required]` | UTC timestamp of submission |
| `ActingUserId` | `string` | `[Required]` `[MaxLength(450)]` | ASP.NET Identity user ID of the submitter — may be the member or a clerk acting on their behalf |

## Indexes

| Columns | Unique | Purpose |
|---------|--------|---------|
| `TeeTimeBookingId` | **Yes** | Enforces one score per booking at the database level — prevents duplicate submissions |

## Invariants

- `Scores` list is always initialized to exactly 18 elements on construction; elements are `null` until entered.
- A `GolfRound` may only be stored once all 18 scores are non-null and in range — enforced by the service before persisting.
- `BookingMemberId` must match `TeeTimeBooking.BookingMemberId` — validated by the service; not a DB constraint.
- `SubmittedAt` is set by the service at submission time (server-side UTC); never supplied by the client.
- `ActingUserId` is set from the authenticated session; never supplied by the client form.

## What Is Not Stored (MVP)

| Field | Reason omitted |
|-------|---------------|
| Course rating | Derivable from `TeeColor` + member gender at calculation time (UC-PS-03) |
| Slope rating | Same — derivable later |
| Par | Display-only in UI; not needed for score storage or handicap calculation |
| Total score | Computable as `Scores.Sum()` — not stored to avoid redundancy |

## Class Definition

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2.Entities.Scoring;

[Index(nameof(TeeTimeBookingId), IsUnique = true)]
public class GolfRound
{
    /// <summary>
    /// Tee colour selected for this round. Nested here because it has no meaning outside a golf round.
    /// </summary>
    public enum TeeColor { Red = 0, White = 1, Blue = 2 }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    [ForeignKey(nameof(TeeTimeBooking))]
    public int TeeTimeBookingId { get; init; }

    [Required]
    public required TeeTimeBooking TeeTimeBooking { get; init; }

    /// <summary>
    /// Plain FK — no navigation property to avoid coupling to the member domain.
    /// </summary>
    [Required]
    public int BookingMemberId { get; init; }

    [Required]
    public TeeColor TeeColor { get; init; }

    /// <summary>
    /// 18 hole scores. Always initialized to length 18; null = not yet entered.
    /// Per-hole range (1–20) is validated in the UI, not via data annotations.
    /// </summary>
    public List<uint?> Scores { get; init; } = Enumerable.Repeat<uint?>(null, 18).ToList();

    [Required]
    public DateTime SubmittedAt { get; init; }

    /// <summary>
    /// Identity user ID of the submitter. May be the member or a clerk acting on their behalf.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public required string ActingUserId { get; init; }
}
```

## Class Relationship Diagram

```mermaid
classDiagram
    class GolfRound {
        +int Id
        +int TeeTimeBookingId
        +TeeTimeBooking TeeTimeBooking
        +int BookingMemberId
        +TeeColor TeeColor
        +List~uint?~ Scores
        +DateTime SubmittedAt
        +string ActingUserId
    }

    class TeeColor {
        <<enumeration>>
        Red
        White
        Blue
    }

    class TeeTimeBooking {
        +int Id
        +DateTime TeeTimeSlotStart
        +int BookingMemberId
        +int ParticipantCount
    }

    class MemberShipInfo {
        +int Id
        +Guid UserId
        +int MembershipLevelId
    }

    GolfRound "1" --> "1" TeeTimeBooking : TeeTimeBookingId (one-way FK)
    GolfRound ..> MemberShipInfo : BookingMemberId (int ref, no nav)
    GolfRound +-- TeeColor : nested enum
```

**Notes:**
- `GolfRound → TeeTimeBooking`: navigation property on `GolfRound` only; `TeeTimeBooking` is not modified.
- `GolfRound ..> MemberShipInfo`: plain `int` FK, no navigation property — avoids coupling to the member domain.
- `TeeColor` is nested inside `GolfRound`; referenced externally as `GolfRound.TeeColor`.
- Unique index on `TeeTimeBookingId` (not shown in diagram) enforces one `GolfRound` per booking.

---

## Relationship to Other Types

- `TeeTimeBooking` — one-way navigation (FK on `GolfRound`; no change to `TeeTimeBooking`)
- `MemberShipInfo` — referenced by `BookingMemberId` only; no navigation property
- `TeeColor` — enum stored as int column

The unique index on `TeeTimeBookingId` is the primary guard against duplicate submissions. The service also checks for an existing `GolfRound` before entering its transaction (fail-fast), with the unique index as the final concurrency safety net.
