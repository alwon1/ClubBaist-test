# Tee Time Reservations – Test Catalog (Phase 1)

## Purpose
List the tests to implement for the tee-time booking service in Phase 1, covering:
- required starting conditions (preconditions),
- expected postconditions,
- and variants for each test group.

Source of truth for `ClubBaist/ClubBaist.Tests` test planning.

## Scope
In scope:
- `TeeTimeBookingService` integration tests (via `TestServiceHost` with in-memory SQLite).
- Individual `IBookingRule` unit tests (pure logic; no DB needed for most rules).
- `DefaultScheduleTimeService` unit tests (pure logic).

Out of scope:
- API/integration tests.
- UI/E2E tests.

## Key Semantics (test authors must know)

- `PlayerMemberAccountIds` on `Reservation`/`TeeTimeSlot` = **additional players only** (not the booking member). Total players = `1 + PlayerMemberAccountIds.Count`.
- Rule return value: negative = denied; `0` = slot exactly full after booking (accepted); positive = remaining capacity.
- `TeeTimeBookingService` returns the **minimum rule result**, clamped to `[0, MaxCapacity]`. Returns `-1` if denied.
- `BookingWindowRule` checks `ISeasonService` — tests must register a season covering the test date.
- `MembershipTimeRestrictionRule` skips when `MemberCategory` is null (availability queries).
- `SlotCapacityRule` excludes `ExcludeReservationId` from occupancy during updates.

---

## Test Group A — Create Reservation (UC-TT-01)

### A1. Create succeeds for valid active member
**Test ID:** UT-TT-CREATE-001
**Starting conditions:**
- Booking member exists and is active.
- Requested date is within an Active or Planned season.
- Requested time is within booking member's membership tier window.
- Slot has remaining capacity.

**Postconditions:**
- Returns `>= 0` (remaining capacity after booking).
- `Reservation` row persisted with correct `SlotDate`, `SlotTime`, `BookingMemberAccountId`.
- `PlayerMemberAccountIds` matches the additional players passed.

**Variants:**
- Gold member, anytime.
- Silver member, allowed time window.
- Bronze member, allowed time window.
- Exactly fills slot (returns `0`).

---

### A2. Create denied — member not found
**Test ID:** UT-TT-CREATE-002
**Postconditions:**
- Returns `-1`. No reservation created.

---

### A3. Create denied — date outside season
**Test ID:** UT-TT-CREATE-003
**Starting conditions:** No season covering the requested date.
**Postconditions:**
- Returns `-1`. No reservation created.

**Variants:**
- Date before any season.
- Date after all seasons closed.
- Exact boundary: day before season start.

---

### A4. Create denied — membership time restriction
**Test ID:** UT-TT-CREATE-004
**Starting conditions:**
- Date is within season.
- Requested time is outside booking member's tier window.

**Postconditions:**
- Returns `-1`. No reservation created.

**Variants:**
- Bronze member, restricted time (weekday 3–6 PM).
- Silver member, restricted time (weekday 3–5:30 PM).
- Social member, any time.

---

### A5. Create denied — slot full
**Test ID:** UT-TT-CREATE-005
**Starting conditions:** Slot currently has 4 players (via existing reservation with 3 additional players + booking member = 4).

**Postconditions:**
- Returns `-1`. No new reservation created.

---

### A6. Create denied — would exceed capacity
**Test ID:** UT-TT-CREATE-006
**Starting conditions:** Slot has 3 players; request adds 2 (total = 5).

**Postconditions:**
- Returns `-1`. No reservation created.

**Variants:**
- 3/4 occupied, request 2 additional (1+2=3 > 1 remaining).
- 2/4 occupied, request 3 additional (1+3=4 > 2 remaining).

---

## Test Group B — Update Reservation (UC-TT-02)

### B1. Update succeeds
**Test ID:** UT-TT-UPDATE-001
**Starting conditions:**
- Reservation exists and is active.
- New player list is valid (capacity + season + time rules pass).

**Postconditions:**
- Returns `>= 0`.
- `Reservation.PlayerMemberAccountIds` updated.

**Variants:**
- Add an additional player.
- Remove an additional player.
- Same player count, different player.

---

### B2. Update denied — reservation not found or cancelled
**Test ID:** UT-TT-UPDATE-002
**Postconditions:** Returns `-1`. No change.

---

### B3. Update denied — capacity exceeded after update
**Test ID:** UT-TT-UPDATE-003
**Starting conditions:**
- Another reservation occupies some slots. Updated list would exceed 4 total.
- `ExcludeReservationId` ensures only the *other* reservations count, not the one being updated.

**Postconditions:**
- Returns `-1`. Reservation unchanged.

---

### B4. Update occupancy excludes the reservation being updated
**Test ID:** UT-TT-UPDATE-004
**Purpose:** Verify `ExcludeReservationId` prevents double-counting.
**Starting conditions:**
- Slot has exactly one reservation (booking member + 2 additional = 3 players).
- Update changes players to booking member + 2 different additional = still 3.

**Postconditions:**
- Returns `>= 0` (slot sees 3 players, not 6). Update persists.

---

## Test Group C — Cancel Reservation (UC-TT-02 Cancel)

### C1. Cancel succeeds
**Test ID:** UT-TT-CANCEL-001
**Postconditions:**
- Returns `true`.
- `Reservation.IsCancelled = true`.
- Subsequent capacity query for that slot increases by the cancelled reservation's player count.

---

### C2. Cancel fails — not found or already cancelled
**Test ID:** UT-TT-CANCEL-002
**Postconditions:** Returns `false`. No change.

---

## Test Group D — Availability Queries

### D1. GetAvailabilityAsync returns correct remaining capacity
**Test ID:** UT-TT-AVAIL-001
**Starting conditions:**
- Season covers the queried date.
- Known set of reservations in the date range.

**Postconditions:**
- Each slot's `RemainingCapacity = 4 - total booked players` (clamped to 0).

**Variants:**
- Single date.
- Multi-day range (verify single DB query via test-level assertion if possible).

---

### D2. Slots outside season return 0 remaining capacity
**Test ID:** UT-TT-AVAIL-002
**Starting conditions:** No season covering queried date.
**Postconditions:**
- All slots have `RemainingCapacity = 0`.

---

### D3. GetBookedTimesAsync returns reservation details
**Test ID:** UT-TT-AVAIL-003
**Postconditions:**
- Returns correct `BookedSlot` list with `Reservations` populated for occupied slots.
- Empty `Reservations` for unbooked slots.

---

## Test Group E — Schedule Time Service

### E1. DefaultScheduleTimeService generates correct intervals
**Test ID:** UT-TT-SCHED-001
**Postconditions:**
- First slot = 7:00 AM.
- Last slot < 7:00 PM.
- Gaps alternate 7/8 minutes. Average = 7.5 minutes.

---

## Test Group F — Capacity Integrity

### F1. Occupancy aggregates correctly across multiple reservations in same slot
**Test ID:** UT-TT-CAP-001
**Starting conditions:** Two active reservations in the same slot.
**Postconditions:** Remaining capacity = `4 - sum of all players across reservations`.

---

### F2. Cancelled reservations are excluded from occupancy
**Test ID:** UT-TT-CAP-002
**Starting conditions:** One active + one cancelled reservation in same slot.
**Postconditions:** Occupancy counts only the active reservation.

---

## Minimal Execution Gate for Initial Implementation
Must include at minimum: A1, A3, A5, A6, B1, B4, C1, C2, D1, D2, F1, F2.

## Definition of Done
A test is complete when it asserts:
1. Starting conditions explicitly set up (season, member, existing reservations).
2. Method return value checked.
3. Postconditions on persisted state verified (or confirmed unchanged on failure paths).
