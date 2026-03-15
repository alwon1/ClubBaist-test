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
- `LocalDate PlayDate`
- `LocalTime TeeTime`
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
- `int MinPlayers`
- `int MaxPlayers`

> Phase 1 note: no cancellation-cutoff policy is enforced yet. If a cutoff is introduced in a later phase, the policy model will be extended at that time.

## Dependencies on Other Services
- Depends on `SeasonService` to confirm play date is inside an active season.
- Read-only dependency on reservation repository/query service for booking ownership/state checks.

## Core Validation / Business Rules
- Booking creation is allowed only when `PlayDate` falls within an active season date range.
- Number of players is derived from reservation participant identities and must remain between 1 and 4.
- Member can cancel only their own active booking.
- Phase 1 cancellation policy has **no time-based cutoff**; valid owner/staff cancellations are allowed regardless of how close `RequestedAt` is to tee time.
- Decision output must include at least one reason when `Allowed = false`.

## Decision / Reason Codes (Phase 1)
- `BOOKING_ALLOWED`: Create or cancel request passed all Phase 1 checks.
- `BOOKING_WINDOW_VIOLATION`: Requested play date is outside the active season window.
- `PLAYER_COUNT_OUT_OF_RANGE`: Participant count is below minimum or above maximum.
- `BOOKING_NOT_FOUND_OR_NOT_ACTIVE`: Booking does not exist or is already canceled/inactive.
- `BOOKING_FORBIDDEN`: Requesting actor is not permitted to maintain the booking.

> `CANCELLATION_CUTOFF_EXCEEDED` is intentionally not used in Phase 1 because cutoff enforcement is deferred.

## POCO note
- `Reservation` is consumed as a POCO record (including `PlayerMemberAccountIds`) with no behavioral methods required by policy evaluation.
- `BookingPolicyService` computes rule outcomes from request + repository state and returns decision codes directly.

## Error / Result Model
- **Success**: `Result<T>.Success(BookingPolicyDecision)`.
- **Validation failure**: `Result<T>.ValidationFailed(errors)` (bad identifiers, invalid participant count, malformed date/time inputs).
- **Conflict**: `Result<T>.Conflict(code, message)` (booking already canceled, season unavailable, stale booking state).
