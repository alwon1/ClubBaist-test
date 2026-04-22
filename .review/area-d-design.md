# Area D: Player Scoring — Design Analysis

## Summary

The `GolfRound` entity is the most structurally incomplete entity in the codebase relative to its business requirements. It is missing the fields that WHS requires (course rating, slope rating, gender), architecturally prevents external course scoring through its mandatory `TeeTimeBooking` FK, and has no path to handicap index calculation. These are not UI gaps — they require model changes that would cascade through service, migration, and UI layers. The entity needs to grow to become a proper WHS-compliant round record before any of the downstream business features (handicap report, external courses, 9-hole rounds) can be implemented.

---

## GolfRound Entity Issues

### Missing WHS-Required Fields
WHS differential formula: `(AdjustedGrossScore − CourseRating) × 113 / SlopeRating`

Current `GolfRound` has: `TeeTimeBookingId`, `MembershipId`, `SelectedTeeColor`, `Scores` (JSON), `SubmittedAt`, `ActingUserId`

Missing required per WHS spec:

| Field | Why Required | Proposed Type |
|---|---|---|
| `CourseRating` | WHS differential denominator | `decimal` (1 dp, e.g. 70.9) |
| `SlopeRating` | WHS differential denominator | `int` (e.g. 127) |
| `Gender` | Determines which CR/SR row to use | `enum Gender { Male, Female, Other }` |
| `CourseId` | Links to the course played | `int` FK → new `Course` entity |
| `TotalScore` | Stored for reporting; currently derived | `int` (sum of `Scores`) |
| `AdjustedGrossScore` | WHS uses adjusted score, not raw | `int` (post-ESC/net double bogey adjustment) |

### Mandatory TeeTimeBooking FK Prevents External Courses
`GolfRound.TeeTimeBookingId` is a non-nullable FK. A round at a Golf Canada-approved external course has no `TeeTimeBooking`. Fix: make `TeeTimeBookingId` nullable (`int?`). External rounds have `TeeTimeBookingId = null` and a populated `CourseId` instead of relying on the booking's slot to determine the course.

### JSON Scores Simplification
`Scores` is stored as `List<uint?>` JSON using a custom `ValueConverter` + `ValueComparer` (verbose 6-line boilerplate). EF Core 8+ supports primitive collection mapping natively. In .NET 10:

```csharp
// Replace the custom HasConversion block with:
entity.Property(r => r.Scores).HasColumnType("nvarchar(max)");
```

For `List<uint?>`, EF Core 8+ primitive collection mapping handles the JSON serialisation automatically when the property is a supported primitive collection type. A migration round-trip test against existing data is required before removing the old converter.

### MinDuration Duplication
`ScoreService.MinDuration(int participantCount)` and `ScoreConsole.razor` both contain the same `switch` expression:
```
1 player → 2h, 2 → 2.5h, 3 → 3h, 4+ → 3.5h
```
This should live in one place — either as a `static` method on `GolfRound` (where it's a domain rule) or as a `static class ScoringRules` in the Domain project. The UI should call the domain method, not reimplement it.

---

## External Course Support Design

### New `Course` Entity

```csharp
public class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsHomeClub { get; set; }       // true for Club BAIST
    public bool IsGolfCanadaApproved { get; set; }

    // Ratings per tee
    public ICollection<CourseRating> Ratings { get; set; } = [];
}

public class CourseRating
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public TeeColor TeeColor { get; set; }
    public Gender Gender { get; set; }
    public decimal CourseRating { get; set; }
    public int SlopeRating { get; set; }
}
```

`GolfRound` gets a `CourseId` FK. When the round is at Club BAIST via a booking, `CourseId` is the home club's ID and `TeeTimeBookingId` is populated. For away rounds, `TeeTimeBookingId` is null.

---

## WHS Compliance Data Model

### Minimum Viable WHS Round Record
After changes, `GolfRound` should carry:

```csharp
public class GolfRound
{
    public int Id { get; set; }
    public int MembershipId { get; set; }          // who played
    public int? TeeTimeBookingId { get; set; }     // nullable — absent for away rounds
    public int CourseId { get; set; }              // which course
    public TeeColor SelectedTeeColor { get; set; }
    public Gender Gender { get; set; }
    public List<uint?> Scores { get; set; }        // hole-by-hole, 18 elements
    public int TotalScore { get; set; }            // stored, not derived
    public int AdjustedGrossScore { get; set; }    // ESC-adjusted
    public decimal CourseRatingUsed { get; set; }  // denormalised at submit time
    public int SlopeRatingUsed { get; set; }       // denormalised at submit time
    public DateTime SubmittedAt { get; set; }
    public string ActingUserId { get; set; } = "";
}
```

Denormalise CR/SR onto the round at submission time so that if the course ratings are updated, historical differentials remain accurate.

---

## Proposed New Entities

1. **`Course`** — represents a playable golf course (home or external)
2. **`CourseRating`** — CR/SR per tee colour per gender per course
3. **`HandicapRecord`** (future) — computed handicap index per member per calculation date; stores the 20-round window and best-8 average

---

## Service Design Issues

1. **`ScoreService.GetEligibleBookingsAsync`** — returns `TeeTimeBooking` objects. Should return a projection or a purpose-built type that includes the computed `MinDuration` threshold for the UI to display.
2. **`ScoreService.SubmitRoundAsync`** — validates the booking exists and belongs to the member, but once external courses are supported, there is no booking to validate against. The validation path needs to branch on whether `TeeTimeBookingId` is present.
3. **`ScoreConsole.razor` date filter** — the `MinDuration` switch is duplicated here and should be removed once the domain has the single source of truth.
4. **No attestation service** — the spec says "scores processed by a clerk." There is no `AttestRoundAsync` or similar method. Attestation should be a separate state transition (`Submitted → Attested`) not a direct query from the console.

---

## Convention Opportunities

- The unique constraint on `GolfRound(TeeTimeBookingId, MembershipId)` should be added to `OnModelCreating` (currently enforced only by application logic).
- Once `TeeTimeBookingId` is nullable, the unique constraint becomes a filtered unique index: `WHERE TeeTimeBookingId IS NOT NULL`.
- `CourseRating` on `GolfRound` should be stored as `decimal(4,1)` in SQL, not `nvarchar` — use `HasColumnType("decimal(4,1)")`.

---

## Notes

- The par array in `RecordScore.razor` (hardcoded C# array) should move to the `Course` entity as `CourseHole` records with `HoleNumber`, `Par`, `StrokeIndexMen`, `StrokeIndexWomen` fields. This unblocks the dual-par holes (7 and 17) and makes par rendering data-driven.
- Total score calculation is currently `Scores.Sum(s => s ?? 0)` in the UI. Once stored in the entity, `SaveChangesAsync` should compute it from the scores array before persisting.
