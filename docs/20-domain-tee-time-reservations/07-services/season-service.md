# SeasonService

## Responsibility
`SeasonService` owns the tee-time season calendar used by booking and availability flows, including identifying the active season for a date, exposing season boundaries, and enforcing season-state constraints so downstream services can work directly with domain models without re-implementing calendar policy.

## Public Operations
- `CreateSeasonAsync(string name, LocalDate startDate, LocalDate endDate, int advanceBookingDays, CancellationToken ct)`
- `GetSeasonForDateAsync(LocalDate playDate, CancellationToken ct)`
- `GetCurrentSeasonAsync(LocalDate today, CancellationToken ct)`
- `CloseSeasonAsync(Guid seasonId, LocalDate closedOn, CancellationToken ct)`

## Inputs / Outputs (domain model contracts)

### CreateSeasonAsync
**Input**
- `string name`
- `LocalDate startDate`
- `LocalDate endDate`
- `int advanceBookingDays`

**Output model: `Season`**
- `Guid SeasonId`
- `string Name`
- `LocalDate StartDate`
- `LocalDate EndDate`
- `int AdvanceBookingDays`
- `SeasonStatus Status` (`Planned | Active | Closed`)

### GetSeasonForDateAsync / GetCurrentSeasonAsync
**Input**
- `LocalDate playDate` or `LocalDate today`

**Output model: `Season`**
- `Guid SeasonId`
- `LocalDate StartDate`
- `LocalDate EndDate`
- `int AdvanceBookingDays`
- `SeasonStatus Status`

### CloseSeasonAsync
**Input**
- `Guid seasonId`
- `LocalDate closedOn`

**Output model: `Season`** (updated status)

## Dependencies on Other Services
- No hard dependency on other domain services in Phase 1.
- Exposes season data consumed by `BookingPolicyService` and `AvailabilityService`.

## Core Validation / Business Rules
- `startDate` must be on or before `endDate`.
- A new season cannot overlap date ranges of existing seasons.
- `advanceBookingDays` must be a positive value.
- A closed season cannot be reopened in Phase 1.
- A date can map to at most one active season.

## Error / Result Model
- **Success**: `Result<T>.Success(payload)` with `Season`.
- **Validation failure**: `Result<T>.ValidationFailed(errors)` (invalid dates, non-positive booking window, missing name).
- **Conflict**: `Result<T>.Conflict(code, message)` (overlapping season, close already-closed season).
