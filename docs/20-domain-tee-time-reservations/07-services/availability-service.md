# AvailabilityService

## Responsibility
`AvailabilityService` provides bookable tee-time views by combining tee-sheet configuration with current reservation occupancy, returning slot-level availability for a single day and across date ranges so daily, weekly, and multi-day planner experiences can use the same domain service.

## Public Operations
- `GetDailyAvailabilityAsync(Guid courseId, LocalDate playDate, int? partySize, CancellationToken ct)`
- `GetAvailabilityRangeAsync(Guid courseId, LocalDate startDate, LocalDate endDate, int? partySize, CancellationToken ct)`
- `GetSlotAvailabilityAsync(Guid courseId, LocalDate playDate, LocalTime teeTime, CancellationToken ct)`
- `CheckCapacityAsync(Guid courseId, LocalDate playDate, LocalTime teeTime, int requestedPlayers, CancellationToken ct)`

## Inputs / Outputs (domain model contracts)

### GetDailyAvailabilityAsync
**Input**
- `Guid courseId`
- `LocalDate playDate`
- `int? partySize`

**Output model: `CourseDayAvailability`**
- `Guid CourseId`
- `LocalDate PlayDate`
- `IReadOnlyList<TeeTimeSlotAvailability> Slots`

### GetAvailabilityRangeAsync
**Input**
- `Guid courseId`
- `LocalDate startDate`
- `LocalDate endDate`
- `int? partySize`

**Output model: `CourseAvailabilityRange`**
- `Guid CourseId`
- `LocalDate StartDate`
- `LocalDate EndDate`
- `IReadOnlyList<CourseDayAvailability> Days`

### GetSlotAvailabilityAsync
**Input**
- `Guid courseId`
- `LocalDate playDate`
- `LocalTime teeTime`

**Output model: `TeeTimeSlotAvailability`**
- `LocalTime TeeTime`
- `int Capacity`
- `int ReservedPlayers`
- `int RemainingPlayers`
- `bool IsBookable`

### CheckCapacityAsync
**Input**
- `Guid courseId`
- `LocalDate playDate`
- `LocalTime teeTime`
- `int requestedPlayers`

**Output contract (no dedicated DTO/class in Phase 1)**
- `bool Fits`
- `int RemainingAfterRequest`
- `string DecisionCode`

## Dependencies on Other Services
- Depends on `SeasonService` to ensure queried dates are in an active season.
- Depends on `BookingPolicyService` for policy-level constraints when computing `IsBookable`.
- Depends on tee-sheet and reservation read models (repositories/query services).

## Core Validation / Business Rules
- Availability can be returned only for dates in an active season.
- `startDate` must be on or before `endDate` for range queries.
- Range query size is limited to 7 days in Phase 1 to support weekly/multi-day planner views without overloading reads.
- Slot capacity cannot drop below zero after confirmed reservations are counted.
- `requestedPlayers` must be greater than zero and no more than slot capacity.
- `IsBookable` requires both free capacity and a positive policy decision.
- Slot-level calculations must be deterministic for the same read timestamp.
- Tee-time slot generation should use the tee-sheet cadence configured for the course/day. Phase 1 default cadence is **approximately 8-minute intervals** (not a hard-coded 7.5-minute requirement).

## Error / Result Model
- **Success**: `Result<T>.Success(payload)` with `CourseDayAvailability`, `CourseAvailabilityRange`, `TeeTimeSlotAvailability`, or an inline capacity-check payload (`Fits`, `RemainingAfterRequest`, `DecisionCode`).
- **Validation failure**: `Result<T>.ValidationFailed(errors)` (invalid course/date/time, non-positive requested players, invalid range).
- **Conflict**: `Result<T>.Conflict(code, message)` (season closed, slot no longer available at check time, stale read version).
