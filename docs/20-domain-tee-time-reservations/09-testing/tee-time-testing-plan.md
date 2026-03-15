# Tee Time Reservations – Service Unit Test Catalog (Phase 1)

## Purpose
List the **unit tests to implement** for tee-time reservation services in Phase 1, including:
- required starting conditions (preconditions),
- expected postconditions,
- and variants for each test group.

This is the source planning document to use before implementing/expanding tests in `ClubBaist/ClubBaist.Tests`.

## Scope
In scope:
- Service-library unit tests only.
- `BookingPolicyService` and reservation service orchestration paths.
- Capacity, authorization, season window, membership-time window, concurrency, and idempotency service behavior.

Out of scope:
- Domain-only unit tests.
- API/integration tests.
- UI/E2E tests.

---

## Test Group A — Create Reservation Unit Tests (UC-TT-01)

### A1. Create reservation succeeds for valid active member
**Test ID:** UT-TT-CREATE-001  
**Starting conditions:**
- Booking member exists and is active.
- Requested play date is in season window.
- Requested tee time is within booking member membership time window.
- Target slot has enough remaining capacity.
- Player count is within allowed min/max.

**Postconditions:**
- Service returns success.
- Reservation entity is created.
- Player entries are linked to reservation.
- Slot occupancy increases by requested player count.
- Decision/result includes `BOOKING_ALLOWED`.

**Variants:**
- Gold member valid window.
- Silver member valid window.
- Bronze member valid window.
- Player counts at lower boundary and upper boundary (still valid).

---

### A2. Create fails for inactive member
**Test ID:** UT-TT-CREATE-002  
**Starting conditions:**
- Booking member exists but is inactive.
- All other inputs are valid (season/time/capacity/player count).

**Postconditions:**
- Service returns failure.
- No reservation created.
- No occupancy change.
- Result reason is `BOOKING_FORBIDDEN` (or inactive-member denial code if introduced later).

**Variants:**
- Actor is the member.
- Actor is staff booking on behalf of member (still denied because booking member is inactive).

---

### A3. Create fails when outside season/booking window
**Test ID:** UT-TT-CREATE-003  
**Starting conditions:**
- Booking member is active.
- Requested play date is outside allowed booking/season window.
- Other inputs valid.

**Postconditions:**
- Service returns failure.
- No reservation created.
- No occupancy change.
- Result reason includes `BOOKING_WINDOW_VIOLATION`.

**Variants:**
- Date before opening boundary.
- Date after closing boundary.
- Exact boundary tests (first allowed day / last allowed day).

---

### A4. Create fails when membership time window is violated
**Test ID:** UT-TT-CREATE-004  
**Starting conditions:**
- Booking member is active.
- Date is valid in season.
- Requested tee time is outside booking member membership-type allowed time window.
- Capacity exists.

**Postconditions:**
- Service returns failure.
- No reservation created.
- No occupancy change.
- Result reason includes `BOOKING_WINDOW_VIOLATION` (or membership-time specific code if added).

**Variants:**
- Bronze early-time denial.
- Silver restricted-time denial.
- Staff-assisted booking where acting user window differs from booking member (must evaluate booking member rules).

---

### A5. Create fails when slot is full
**Test ID:** UT-TT-CREATE-005  
**Starting conditions:**
- Booking member is active and otherwise eligible.
- Target slot current occupancy is 4/4.
- Requested player count >= 1.

**Postconditions:**
- Service returns capacity failure/conflict.
- No reservation created.
- Occupancy remains 4/4.
- Response includes remaining capacity = 0.

**Variants:**
- 1-player request into full slot.
- 2-player request into full slot.

---

### A6. Create fails when requested players exceed remaining capacity
**Test ID:** UT-TT-CREATE-006  
**Starting conditions:**
- Slot occupancy is partial (e.g., 3/4 or 2/4).
- Booking member otherwise valid.
- Requested player count exceeds remaining capacity.

**Postconditions:**
- Service returns capacity failure/conflict.
- No reservation created.
- Occupancy unchanged.
- Response includes correct remaining capacity.

**Variants:**
- Occupancy 3/4, request 2.
- Occupancy 2/4, request 3.

---

### A7. Create fails for invalid player count range
**Test ID:** UT-TT-CREATE-007  
**Starting conditions:**
- Member and slot conditions otherwise valid.
- Player count is outside policy range.

**Postconditions:**
- Service returns validation/policy failure.
- No reservation created.
- No occupancy change.
- Result reason includes `PLAYER_COUNT_OUT_OF_RANGE`.

**Variants:**
- Player count below minimum.
- Player count above maximum.

---

### A8. Staff-assisted create success (authorized)
**Test ID:** UT-TT-CREATE-008  
**Starting conditions:**
- Acting user has authorized staff role.
- Booking member is active.
- Booking member season/time eligibility passes.
- Capacity available.

**Postconditions:**
- Service returns success.
- Reservation owner/member linkage uses booking member.
- Acting user metadata is recorded.
- Occupancy increases correctly.

**Variants:**
- Staff creates for Gold member.
- Staff creates for Bronze member at allowed time.

---

### A9. Create idempotency behavior
**Test ID:** UT-TT-CREATE-009  
**Starting conditions:**
- First create command has unique idempotency key and succeeds.
- Same command is replayed with same key.

**Postconditions:**
- No duplicate reservation created.
- No duplicate player rows.
- Occupancy increment applied once.
- Second call returns stable duplicate-safe result.

**Variants:**
- Immediate replay.
- Replay after simulated transient timeout.

---

## Test Group B — Update/Move Reservation Unit Tests (UC-TT-02 Update)

### B1. Update succeeds when all rules pass
**Test ID:** UT-TT-UPDATE-001  
**Starting conditions:**
- Reservation exists and is active.
- Actor is owner or authorized staff.
- Target date/time is season-valid and membership-time valid for booking member.
- Target slot has sufficient remaining capacity.
- Version token is current.

**Postconditions:**
- Service returns success (`BOOKING_ALLOWED`).
- Reservation details updated.
- Source slot occupancy decremented by old player count (as applicable).
- Target slot occupancy incremented by new player count.
- Net occupancy integrity preserved.

**Variants:**
- Same slot, player list change only.
- Move to different slot, same player count.
- Move + player count change within capacity.

---

### B2. Update denied for unauthorized actor
**Test ID:** UT-TT-UPDATE-002  
**Starting conditions:**
- Reservation exists and active.
- Actor is not owner and lacks staff role.
- Other inputs valid.

**Postconditions:**
- Service returns failure with `BOOKING_FORBIDDEN`.
- Reservation unchanged.
- Occupancies unchanged.

**Variants:**
- Unauthorized update attempt.
- Unauthorized move attempt.

---

### B3. Update fails when target slot lacks capacity
**Test ID:** UT-TT-UPDATE-003  
**Starting conditions:**
- Reservation exists and actor authorized.
- Proposed update targets slot without enough remaining capacity.

**Postconditions:**
- Service returns capacity conflict/failure.
- Reservation unchanged.
- Source and target occupancies unchanged.

**Variants:**
- Target full (4/4).
- Target partial but still insufficient for requested players.

---

### B4. Update fails for season/time-window violations
**Test ID:** UT-TT-UPDATE-004  
**Starting conditions:**
- Reservation exists and actor authorized.
- Proposed date/time violates season or membership-time rules.

**Postconditions:**
- Service returns failure with `BOOKING_WINDOW_VIOLATION`.
- Reservation unchanged.
- Occupancies unchanged.

**Variants:**
- Out-of-season target date.
- Membership-time restricted target time.

---

### B5. Update fails on stale version conflict
**Test ID:** UT-TT-UPDATE-005  
**Starting conditions:**
- Two update requests start from same reservation version.
- First request commits successfully.
- Second request executes with stale version.

**Postconditions:**
- First succeeds.
- Second returns conflict result.
- No partial occupancy drift from failed stale update.

**Variants:**
- Competing updates to same target slot.
- Competing updates to different target slots.

---

### B6. Update/move rollback on injected persistence failure
**Test ID:** UT-TT-UPDATE-006  
**Starting conditions:**
- Reservation exists; actor authorized; update initially valid.
- Inject failure between occupancy decrement and increment (or equivalent transaction step).

**Postconditions:**
- Service returns failure/conflict.
- Transaction rolls back fully.
- Reservation remains unchanged.
- Source/target occupancies remain pre-operation values.

**Variants:**
- Failure after source decrement.
- Failure during reservation update write.

---

## Test Group C — Cancel Reservation Unit Tests (UC-TT-02 Cancel)

### C1. Cancel succeeds for authorized owner
**Test ID:** UT-TT-CANCEL-001  
**Starting conditions:**
- Reservation exists and is active.
- Actor is reservation owner.

**Postconditions:**
- Service returns success with `BOOKING_ALLOWED`.
- Reservation status becomes canceled/inactive.
- Occupancy decreases by reserved player count.

**Variants:**
- Cancel far before tee time.
- Cancel close to tee time (still allowed in Phase 1).

---

### C2. Cancel succeeds for authorized staff
**Test ID:** UT-TT-CANCEL-002  
**Starting conditions:**
- Reservation exists and active.
- Actor has authorized staff role.

**Postconditions:**
- Service returns success.
- Reservation canceled.
- Occupancy released exactly once.

**Variants:**
- Staff cancel on behalf of member.

---

### C3. Cancel denied for unauthorized actor
**Test ID:** UT-TT-CANCEL-003  
**Starting conditions:**
- Reservation exists and active.
- Actor is neither owner nor authorized staff.

**Postconditions:**
- Service returns `BOOKING_FORBIDDEN`.
- Reservation remains active.
- Occupancy unchanged.

**Variants:**
- Unauthorized member actor.

---

### C4. Cancel fails when reservation missing or not active
**Test ID:** UT-TT-CANCEL-004  
**Starting conditions:**
- Reservation does not exist, or exists but already canceled.

**Postconditions:**
- Service returns `BOOKING_NOT_FOUND_OR_NOT_ACTIVE`.
- No occupancy side effects.

**Variants:**
- Missing reservation ID.
- Already canceled reservation.

---

### C5. Cancel has no cutoff rejection in Phase 1
**Test ID:** UT-TT-CANCEL-005  
**Starting conditions:**
- Reservation active and actor authorized.
- Cancel attempt occurs very close to tee time.

**Postconditions:**
- Service does not emit `CANCELLATION_CUTOFF_EXCEEDED`.
- If other rules pass, cancellation succeeds.

**Variants:**
- Cancel at configured boundary minutes before tee time.
- Cancel after prior update near tee time.

---

### C6. Cancel idempotency / repeated command safety
**Test ID:** UT-TT-CANCEL-006  
**Starting conditions:**
- First cancel succeeds.
- Same cancel command is replayed.

**Postconditions:**
- Replay yields stable terminal response (not-active or duplicate-safe).
- Occupancy decrement occurs once only.
- No additional state corruption.

**Variants:**
- Immediate replay.
- Replay after transient error simulation.

---

## Test Group D — Shared Slot Capacity Integrity Unit Tests

### D1. Shared slot occupancy aggregates across reservations
**Test ID:** UT-TT-CAP-001  
**Starting conditions:**
- Multiple reservations in same slot with known counts.

**Postconditions:**
- Service-calculated occupancy equals sum of active reservation players.
- Remaining capacity is `4 - occupancy`.

**Variants:**
- Two reservations sharing a slot.
- Three reservations sharing a slot.

---

### D2. Capacity never exceeds 4 under normal sequential operations
**Test ID:** UT-TT-CAP-002  
**Starting conditions:**
- Sequence of create/update/cancel operations with deterministic ordering.

**Postconditions:**
- Occupancy never exceeds 4.
- Final occupancy matches expected arithmetic from operations.

**Variants:**
- Create then cancel then create.
- Move-in/move-out sequences across two slots.

---

### D3. Capacity integrity under concurrent competing writes
**Test ID:** UT-TT-CAP-003  
**Starting conditions:**
- Two or more competing operations target same slot near capacity.

**Postconditions:**
- At most one conflicting operation succeeds when needed.
- Final occupancy <= 4.
- Failed operations return conflict/capacity failures without partial writes.

**Variants:**
- Competing creates into remaining capacity 1.
- Competing update-moves into nearly full slot.

---

## Minimal Execution Gate for Initial Implementation
The first implementation pass in `ClubBaist/ClubBaist.Tests` must include at minimum:
- A1, A2, A3, A5, A7, A8
- B1, B2, B3, B5, B6
- C1, C3, C4, C5
- D1, D3

(Equivalent consolidated tests are acceptable if they still prove all listed preconditions/postconditions.)

## Definition of Done for This Plan
A unit test is considered complete only when it explicitly asserts:
1. Starting condition setup (member status, window eligibility, authorization role, slot occupancy, version/idempotency key as applicable).
2. Service result semantics (success/failure/conflict + reason code assertions).
3. Postconditions on reservation state and slot occupancy.
4. Negative side-effect checks (unchanged state on failure paths).

## Adoption Rule for `ClubBaist/ClubBaist.Tests`
Before adding/expanding tee-time unit tests:
1. Map each test implementation to the UT-TT test IDs above.
2. Keep test names aligned with these IDs for traceability.
3. Update this document first (or in the same PR) if behavior or expected reason codes change.
