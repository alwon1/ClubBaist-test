# TeeColor (Enum — nested in GolfRound)

## Purpose

Represents the tee colour a member selects when recording a round. Defined as a nested enum inside `GolfRound` — it has no meaning outside of a golf round.

Tee colour is the only round-level metadata stored in MVP and serves as the future lookup key for course rating and slope rating when UC-PS-03 (Handicap Index) is implemented.

## Values

| Value | Meaning |
|-------|---------|
| `Red` | Red tees |
| `White` | White tees |
| `Blue` | Blue tees |

## Location

Declared inside the `GolfRound` class. Referenced as `GolfRound.TeeColor` from outside the class.

See `golf-round-class.md` for the full class definition including this enum.
