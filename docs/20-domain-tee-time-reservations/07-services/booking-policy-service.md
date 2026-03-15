# BookingPolicyService

## Responsibility
`BookingPolicyService` evaluates whether a member can create or modify a tee-time booking under Phase 1 policy rules, centralizing lead-time, party-size, and booking-state checks so reservation commands receive one domain-level policy decision.

## Public Operations
- `EvaluateCreateBookingAsync(BookingRequest bookingRequest, CancellationToken ct)`
- `EvaluateCancelBookingAsync(BookingCancellation bookingCancellation, CancellationToken ct)`
- `GetPolicyForDateAsync(LocalDate playDate, CancellationToken ct)`

## Inputs / Outputs (domain model contracts)

### EvaluateCreateBookingAsync
**Input model: `BookingRequest`**
- `Guid MemberId`
- `Guid CourseId`
- `LocalDate PlayDate`
- `LocalTime TeeTime`
- `int PlayerCount`
- `DateTimeOffset RequestedAt`

**Output model: `BookingPolicyDecision`**
- `bool Allowed`
- `string DecisionCode`
- `IReadOnlyList<string> Reasons`
- `BookingPolicy PolicyApplied`

### EvaluateCancelBookingAsync
**Input model: `BookingCancellation`**
- `Guid BookingId`
- `Guid MemberId`
- `DateTimeOffset RequestedAt`

**Output model: `BookingPolicyDecision`**
- `bool Allowed`
- `string DecisionCode`
- `IReadOnlyList<string> Reasons`

### GetPolicyForDateAsync
**Input**
- `LocalDate playDate`

**Output model: `BookingPolicy`**
- `Guid SeasonId`
- `int AdvanceBookingDays`
- `int MinPlayers`
- `int MaxPlayers`
- `Duration CancellationCutoff`

## Dependencies on Other Services
- Depends on `SeasonService` for season-specific booking window configuration.
- Read-only dependency on reservation repository/query service for booking ownership/state checks.

## Core Validation / Business Rules
- Booking creation is allowed only when `PlayDate` is within the season’s advance-booking window.
- `PlayerCount` must be within configured min/max limits.
- Member can cancel only their own active booking.
- Cancellation requests after the cutoff window are rejected.
- Decision output must include at least one reason when `Allowed = false`.

## Error / Result Model
- **Success**: `Result<T>.Success(BookingPolicyDecision)`.
- **Validation failure**: `Result<T>.ValidationFailed(errors)` (bad identifiers, invalid player count, malformed date/time inputs).
- **Conflict**: `Result<T>.Conflict(code, message)` (booking already canceled, season unavailable, stale booking state).
