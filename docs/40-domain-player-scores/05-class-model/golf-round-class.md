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
| `MemberId` | `int` | `[Required]` `[ForeignKey(nameof(Member))]` | FK to `MemberShipInfo` |
| `Member` | `MemberShipInfo` | `[Required]` | Navigation property — required for EF Core to map the FK correctly |
| `TeeColor` | `TeeColor` | `[Required]` | Tee colour chosen for this round; future lookup key for course/slope ratings |
| `Scores` | `List<uint?>` | *(none)* | 18 nullable unsigned ints, initialized to length 18. Null = hole not yet entered. All 18 scores must be non-null and in range (1–20) before persisting, enforced by the service; UI validation may also exist for UX. |
| `SubmittedAt` | `DateTime` | `[Required]` | Server-side timestamp of submission (set by the service, never from the client) |
| `ActingUserId` | `string` | `[Required]` `[MaxLength(450)]` | ASP.NET Identity user ID of the submitter — may be the member or a clerk acting on their behalf |

## Indexes

| Columns | Unique | Purpose |
|---------|--------|---------|
| `TeeTimeBookingId`, `MemberId` | **Yes** | Enforces one score per booking per member at the database level — not a composite PK because future external-course rounds (UC-PS-02) may share a booking reference across members |

## Invariants

- `Scores` list is always initialized to exactly 18 elements on construction; elements are `null` until entered.
- A `GolfRound` may only be stored once all 18 scores are non-null and in range (1–20) — enforced by the service before persisting.
- `MemberId` must match `TeeTimeBooking.BookingMemberId` — validated by the service; not a DB constraint.
- `SubmittedAt` is set by the service at submission time (server-side); never supplied by the client.
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
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2.Entities.Scoring;

[Index(nameof(TeeTimeBookingId), nameof(MemberId), IsUnique = true)]
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

    [Required]
    [ForeignKey(nameof(Member))]
    public int MemberId { get; init; }

    [Required]
    public required MemberShipInfo Member { get; init; }

    [Required]
    public TeeColor TeeColor { get; init; }

    /// <summary>
    /// 18 hole scores. Always initialized to length 18; null = not yet entered.
    /// Service enforces all 18 non-null and in range 1–20 before persisting.
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
        +int MemberId
        +MemberShipInfo Member
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
    GolfRound "1" --> "1" MemberShipInfo : MemberId (FK + nav property)
    GolfRound +-- TeeColor : nested enum
```

**Notes:**
- `GolfRound → TeeTimeBooking`: navigation property on `GolfRound` only; `TeeTimeBooking` is not modified.
- `GolfRound → MemberShipInfo`: FK + navigation property — required for EF Core to map the relationship correctly.
- `TeeColor` is nested inside `GolfRound`; referenced externally as `GolfRound.TeeColor`.
- Composite unique index on `(TeeTimeBookingId, MemberId)` — not a composite PK because UC-PS-02 (external courses) may eventually require multiple member scores per booking reference.

---

## Relationship to Other Types

- `TeeTimeBooking` — one-way navigation (FK on `GolfRound`; no change to `TeeTimeBooking`)
- `MemberShipInfo` — FK + navigation property (`MemberId` / `Member`)
- `TeeColor` — nested enum, stored as int column

The composite unique index on `(TeeTimeBookingId, MemberId)` is the primary guard against duplicate submissions. The service also checks for an existing `GolfRound` before entering its transaction (fail-fast), with the unique index as the final concurrency safety net.
