# Standing Tee Time – Test Catalog (Phase 1)

## Purpose
Catalog the tests implemented for `StandingTeeTimeService` and the `StandingTeeTime` domain model, covering:
- required starting conditions (preconditions),
- expected postconditions,
- and the variant each test covers.

Source of truth maps to tests in `ClubBaist.Domain2.Tests`:
- `StandingTeeTimeServiceTests.cs` — service integration tests
- `StandingTeeTimePhaseOneTests.cs` — domain model and persistence tests

## Scope

**In scope:**
- `StandingTeeTimeService` integration tests (via `Domain2TestHost` with in-memory SQLite).
- `StandingTeeTime` domain model unit tests (pure construction + property assertions).
- EF Core persistence smoke tests (DbContext round-trip + relationship mapping).

**Out of scope:**
- API / controller tests.
- UI / E2E tests.
- Authorization / permission claim enforcement tests (handled by web layer).

---

## Test Group A — SubmitRequestAsync

### A1. Valid request succeeds
**Test ID:** `UT-STT-SUBMIT-001`  
**Test method:** `SubmitRequestAsync_ValidRequest_Succeeds`

**Starting conditions:**
- Booking member exists and is active.
- 3 distinct additional participants exist, none of which is the booking member.
- No existing active standing tee time for the booking member.
- `StartDate < EndDate`.

**Postconditions:**
- Returns `(Success: true, ErrorMessage: null)`.
- One `StandingTeeTime` row persisted with `Status = Draft`.
- Persisted row has correct `BookingMemberId`.

---

### A2. Second active request for same member is rejected
**Test ID:** `UT-STT-SUBMIT-002`  
**Test method:** `SubmitRequestAsync_SecondActiveRequest_ReturnsFalse`

**Starting conditions:**
- Booking member already has one active (Draft) standing tee time request.

**Postconditions:**
- Returns `(Success: false, ErrorMessage: <non-null message>)`.
- No second row inserted.

---

### A3. End date before start date is rejected
**Test ID:** `UT-STT-SUBMIT-003`  
**Test method:** `SubmitRequestAsync_EndDateBeforeStartDate_ReturnsFalse`

**Starting conditions:**
- `EndDate` is before `StartDate` in the request.

**Postconditions:**
- Returns `(Success: false, ErrorMessage: <non-null message>)`.
- No row inserted.

---

### A4. Fewer than 3 additional players is rejected
**Test ID:** `UT-STT-SUBMIT-004`  
**Test method:** `SubmitRequestAsync_FewerThanThreePlayers_ReturnsFalse`

**Starting conditions:**
- `AdditionalParticipants` contains only 2 members (not a foursome).

**Postconditions:**
- Returns `(Success: false, ErrorMessage: <non-null message>)`.
- No row inserted.

---

### A5. Booking member listed as additional participant is rejected
**Test ID:** `UT-STT-SUBMIT-005`  
**Test method:** `SubmitRequestAsync_BookingMemberInParticipants_ReturnsFalse`

**Starting conditions:**
- `AdditionalParticipants` contains the booking member alongside 2 others.

**Postconditions:**
- Returns `(Success: false, ErrorMessage: <non-null message>)`.
- No row inserted.

---

### A6. Duplicate additional participants are rejected
**Test ID:** `UT-STT-SUBMIT-006`  
**Test method:** `SubmitRequestAsync_DuplicateParticipants_ReturnsFalse`

**Starting conditions:**
- `AdditionalParticipants` contains the same member twice (with one distinct member making up 3 entries).

**Postconditions:**
- Returns `(Success: false, ErrorMessage: <non-null message>)`.
- No row inserted.

---

## Test Group B — ApproveAsync

### B1. Approve a Draft request — succeeds
**Test ID:** `UT-STT-APPROVE-001`  
**Test method:** `ApproveAsync_DraftRequest_SetsApprovedStatusAndTime`

**Starting conditions:**
- A valid standing tee time request exists with `Status = Draft`.
- `approvedTime` and `priorityNumber` provided.

**Postconditions:**
- Returns `true`.
- Persisted record has `Status = Approved`.
- `ApprovedTime` matches the provided value.
- `PriorityNumber` matches the provided value.

---

### B2. Approve a non-Draft request — returns false
**Test ID:** `UT-STT-APPROVE-002`  
**Test method:** `ApproveAsync_NonDraftRequest_ReturnsFalse`

**Starting conditions:**
- A standing tee time request exists that has already been approved (`Status = Approved`).

**Postconditions:**
- Returns `false`.
- Record status is unchanged.

---

## Test Group C — DenyAsync

### C1. Deny a Draft request — succeeds
**Test ID:** `UT-STT-DENY-001`  
**Test method:** `DenyAsync_DraftRequest_SetsDeniedStatus`

**Starting conditions:**
- A valid standing tee time request exists with `Status = Draft`.

**Postconditions:**
- Returns `true`.
- Persisted record has `Status = Denied`.

---

### C2. Deny a non-Draft request — returns false
**Test ID:** `UT-STT-DENY-002`  
**Test method:** `DenyAsync_NonDraftRequest_ReturnsFalse`

**Starting conditions:**
- A standing tee time request that has already been approved (`Status = Approved`).

**Postconditions:**
- Returns `false`.
- Record status is unchanged (`Approved`).

---

## Test Group D — CancelAsync

### D1. Cancel own request — succeeds
**Test ID:** `UT-STT-CANCEL-001`  
**Test method:** `CancelAsync_OwnRequest_SetsCancelledStatus`

**Starting conditions:**
- A standing tee time request exists in `Draft` status.
- `requestingMemberId` matches `BookingMemberId`.

**Postconditions:**
- Returns `true`.
- Persisted record has `Status = Cancelled`.

---

### D2. Cancel another member's request — returns false
**Test ID:** `UT-STT-CANCEL-002`  
**Test method:** `CancelAsync_WrongMember_ReturnsFalse`

**Starting conditions:**
- A standing tee time request exists.
- `requestingMemberId` does NOT match `BookingMemberId`.

**Postconditions:**
- Returns `false`.
- Record status is unchanged (`Draft`).

---

### D3. Cancel an already-cancelled request — returns false
**Test ID:** `UT-STT-CANCEL-003`  
**Test method:** `CancelAsync_AlreadyCancelledRequest_ReturnsFalse`

**Starting conditions:**
- A standing tee time request exists with `Status = Cancelled` (previously cancelled in same test).
- `requestingMemberId` matches the original `BookingMemberId`.

**Postconditions:**
- Returns `false`.
- Record status remains `Cancelled`.

---

## Test Group E — Domain Model and Persistence

### E1. Entity tracks participants, computed properties, and defaults
**Test ID:** `UT-STT-MODEL-001`  
**Test method:** `StandingTeeTime_TracksBookingMemberParticipantsAndDefaults`

**Starting conditions:**
- `StandingTeeTime` constructed in-memory with 1 additional participant.
- No database involved.

**Postconditions:**
- `ParticipantCount == 2`.
- `ToleranceMinutes == 30` (default).
- `AppRoles.Claims.StandingTeeTimeBooking.Type` and `.Value` match expected permission constants.
- Additional participant's membership level short code is readable via the navigation.

---

### E2. DbContext persists StandingTeeTime and GeneratedBooking link
**Test ID:** `UT-STT-PERSIST-001`  
**Test method:** `AppDbContext_PersistsStandingTeeTime_AndGeneratedBookingLink`

**Starting conditions:**
- A season and slot exist in the in-memory DB.
- A booking member (Shareholder) and one additional participant exist.
- A `StandingTeeTime` with `Status = Approved` and `PriorityNumber = 5` is saved.
- A `TeeTimeBooking` with `StandingTeeTimeId` referencing the above is saved.

**Postconditions:**
- Querying with `Include(AdditionalParticipants)` returns the correct participant with correct membership level.
- Querying `TeeTimeBookings` filtered by `StandingTeeTimeId` returns exactly 1 booking with the correct `Id`.

---

## Minimal Execution Gate
Must pass at minimum: A1, A2, A4, B1, B2, C1, D1, D2, E1, E2.

## Definition of Done
A test is complete when it asserts:
1. Starting conditions are explicitly set up (member, request, DB state).
2. Method return value or property value is checked.
3. Persisted state is verified after the operation (or confirmed unchanged on failure paths).
