# Phase 1 Service Flows

This document defines the three primary tee-time flows. All booking and availability operations go through `TeeTimeBookingService`, which applies rules from `IBookingRule` implementations.

## 1. Availability Query Flow

### Purpose
Return available tee-time slots for a date or date range.

### Sequence
1. **Caller → API** with date range (`from`, `to`).
2. **API → TeeTimeBookingService.GetAvailabilityAsync(from, to)**
   - Fetches all active reservations for the date range in one query.
   - Computes per-slot occupancy in memory.
   - For each date, retrieves schedule times from `IScheduleTimeService`.
   - For each slot, runs all `IBookingRule` implementations with `MemberCategory = null`.
     - `BookingWindowRule` checks if the date falls in an Active/Planned season via `ISeasonService`.
     - `SlotCapacityRule` uses precomputed occupancy (no per-slot DB query).
     - `MembershipTimeRestrictionRule` passes through when `MemberCategory` is null.
   - Returns `DayAvailability` list with `SlotAvailability(Time, RemainingCapacity)` per slot.
3. **API → Caller** with availability data.

### GetBookedTimesAsync variant
Same as above but returns `BookedSlot` (time, remaining capacity, full reservation list with players) for building a booked-times/sheet view.

---

## 2. Reservation Create Flow

### Purpose
Validate and persist a new tee-time booking.

### Sequence
1. **Caller → API** with `TeeTimeSlot(date, time, bookingMemberId, additionalPlayerIds)`.
2. **API → TeeTimeBookingService.CreateReservationAsync(slot)**
   a. Fetches booking member's `MembershipCategory`. Returns `-1` if member not found.
   b. Opens a **serializable transaction**.
   c. Builds `BookingEvaluationContext(memberCategory)` and runs all rules:
      - `BookingWindowRule` — slot date must be in an Active or Planned season.
      - `SlotCapacityRule` — total players after booking must not exceed 4. `requested = 1 + additionalPlayers.Count`.
      - `MembershipTimeRestrictionRule` — booking member's tier must allow this time of day.
   d. If any rule returns negative → transaction rolled back, returns `-1`.
   e. If all rules pass (result ≥ 0) → persists `Reservation`, commits transaction.
3. **API → Caller** with remaining capacity (0 = exactly full, positive = spots remaining, negative = denied).

### Player counting note
`PlayerMemberAccountIds` on `Reservation` and `TeeTimeSlot` contains **additional players only** (not the booking member). The booking member is always player #1 implicitly. Total players = `1 + PlayerMemberAccountIds.Count`.

---

## 3. Reservation Update / Cancel Flow

### Purpose
Modify the player list on an existing reservation, or cancel it.

### Update sequence
1. **Caller → API** with `reservationId` and new `playerMemberAccountIds` (additional players).
2. **API → TeeTimeBookingService.UpdateReservationAsync(reservationId, playerMemberAccountIds)**
   a. Loads reservation. Returns `-1` if not found or already cancelled.
   b. Fetches booking member's `MembershipCategory`. Returns `-1` if not found.
   c. Opens a **serializable transaction**.
   d. Builds `BookingEvaluationContext(memberCategory, ExcludeReservationId: reservationId)`.
      - `ExcludeReservationId` ensures `SlotCapacityRule` excludes the current reservation from occupancy, preventing double-counting.
   e. Runs all rules. Returns `-1` if any rule denies.
   f. Persists updated `PlayerMemberAccountIds`, commits transaction.
3. **API → Caller** with remaining capacity or `-1` if denied.

### Cancel sequence
1. **Caller → API** with `reservationId`.
2. **API → TeeTimeBookingService.CancelReservationAsync(reservationId)**
   - Loads reservation. Returns `false` if not found or already cancelled.
   - Sets `IsCancelled = true`, persists.
   - Returns `true`.
3. **API → Caller** with `bool` result.

### Concurrency note
Create and update operations use serializable transactions so the capacity check and insert/update are atomic. Two concurrent requests targeting the same near-full slot will not both succeed.
