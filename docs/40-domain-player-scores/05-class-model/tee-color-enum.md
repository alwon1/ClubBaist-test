# TeeColor (Enum)

## Purpose

Represents the tee colour a member selects when recording a round. Tee colour is the only round-level metadata stored in MVP — it serves as the future lookup key for course rating and slope rating when UC-PS-03 (Handicap Index) is implemented.

## Values

| Value | Meaning |
|-------|---------|
| `Red` | Red tees |
| `White` | White tees |
| `Blue` | Blue tees |

## Notes

- Stored as an integer column in the database via EF Core default enum mapping.
- No data annotation is required on the enum itself; `[Required]` is applied on the `GolfRound.TeeColor` property.
- The mapping from `TeeColor` + member gender to course/slope rating is deferred to UC-PS-03. `TeeColor` is stored now so that calculation is possible later without a schema change to `GolfRound`.

## Definition

```csharp
namespace ClubBaist.Domain2.Entities.Scoring;

/// <summary>
/// The tee colour selected by a member for a recorded golf round.
/// Used as the lookup key for course and slope ratings in handicap calculation (UC-PS-03).
/// </summary>
public enum TeeColor
{
    Red = 0,
    White = 1,
    Blue = 2
}
```
