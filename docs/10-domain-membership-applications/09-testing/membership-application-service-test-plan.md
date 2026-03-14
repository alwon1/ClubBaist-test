# Membership Application Service – Test Plan (Draft v2)

## Purpose
Define a DbContext-backed testing plan for the membership application service class library.

## Scope
- `ApplicationManagementService` submission, retrieval, and decision flows.
- `MemberManagementService` behavior invoked by accepted applications.
- Domain model invariants used by membership application workflows.
- Persistence-backed behavior using EF Core + SQLite in-memory.

## Out of Scope (for this library test plan)
- Caller identity/role enforcement (handled by web/API layer authorization).
- End-to-end UI behavior.

---

## Testing Approach (DbContext-Backed)
- Use **SQLite in-memory** (`DataSource=:memory:`) for test execution.
- Keep the SQLite connection open for each test scope so schema/data remain available for assertions.
- Use the concrete EF Core `DbContext` and real entity mappings.
- Do **not** use mocked/faked dependencies for service behavior in this plan.

---

## Service Test Matrix (with Preconditions and Postconditions)

### 1) `SubmitApplicationAsync`

#### What to Test
- Required application fields are present.
- Sponsor requirements are satisfied.
- New application is persisted with initial `Submitted` status.
- Submission metadata (`SubmittedAt`, submitter IDs/trace fields) is recorded.

#### Preconditions
- Database schema is created.
- Request contains all required personal/contact fields.
- Required sponsor references are provided per current rule set.
- Submitter identity value is present (as service input).

#### Postconditions (Success)
- A new `MembershipApplication` row exists.
- `CurrentStatus = Submitted`.
- Submission timestamp is persisted.
- Returned response includes a valid `ApplicationId` and current status.

#### Postconditions (Failure)
- No application row is inserted.
- Validation failure is surfaced in a consistent service error/result shape.

---

### 2) `GetActionableApplicationsAsync`

#### What to Test
- Query returns only actionable statuses (`Submitted`, `OnHold`, `Waitlisted`).
- Optional filters (status/date/paging, if present in implementation) behave correctly.
- Empty queue returns a valid empty collection.

#### Preconditions
- Database seeded with applications across actionable and non-actionable statuses.
- Optional filter parameters prepared with known expected results.

#### Postconditions
- Result set excludes non-actionable statuses (`Accepted`, `Denied`).
- Returned rows match filter inputs and ordering/paging expectations.
- Method does not mutate persisted data.

---

### 3) `ChangeApplicationStatusAsync`

#### What to Test
- Allowed status transitions succeed.
- Disallowed transitions fail.
- Terminal status behavior is enforced (`Accepted`, `Denied`).
- Update metadata (`LastStatusChangedAt`, actor fields) is persisted.

#### Preconditions
- Target application exists.
- Current status and requested new status form a known transition case.
- `changedByUserId` and `changedAt` are provided.

#### Postconditions (Success)
- Application `CurrentStatus` reflects requested valid target status.
- Status change timestamp and actor fields are updated.
- Service returns updated application summary.

#### Postconditions (Failure)
- Application status remains unchanged.
- Failure reason identifies invalid transition/not-found condition.

---

### 4) `RecordStatusHistoryAsync`

#### What to Test
- One history record is created per valid status transition.
- Stored values are correct (`fromStatus`, `toStatus`, `changedByUserId`, `changedAt`).
- No history row is written when transition fails.

#### Preconditions
- Application exists.
- A valid status change event context is available.

#### Postconditions
- `ApplicationStatusHistory` contains the expected new row.
- History entries are attributable and time-stamped for audit.
- No duplicate rows are created for single transition operation.

---

### 5) Acceptance Path (`ChangeApplicationStatusAsync` -> `MemberManagementService.CreateMemberAsync`)

#### What to Test
- Setting status to `Accepted` triggers member-account creation workflow.
- Non-accepted transitions do not create member accounts.
- Failure in member creation is surfaced consistently and leaves data in expected state.

#### Preconditions
- Application exists and is eligible for transition to `Accepted`.
- Linked user/application data needed for member creation exists.

#### Postconditions (Success)
- Application final status is `Accepted`.
- One new `MemberAccount` row exists and is linked correctly.
- Service response includes acceptance/member-creation outcome details.

#### Postconditions (Failure)
- Failure result is returned with clear reason.
- Data consistency is maintained per transaction behavior (no orphan/partial records).

---

### 6) `MemberManagementService.CreateMemberAsync`

#### What to Test
- Required member fields are enforced.
- `ApplicationUserId` existence is required.
- `MemberNumber` uniqueness is enforced.
- Created member is persisted with expected defaults.

#### Preconditions
- Linked application user exists in persistence.
- Request contains required member fields.
- Generated/provided member number path is deterministic for assertions.

#### Postconditions (Success)
- `MemberAccount` row is created with unique `MemberNumber`.
- Output includes `MemberAccountId`, `MemberNumber`, and `CreatedAt`.

#### Postconditions (Failure)
- No member row is created for invalid input/nonexistent user/duplicate member number.
- Error is returned in consistent service result format.

---

### 7) Error and Robustness Scenarios

#### What to Test
- Not-found application IDs in read/update operations.
- Duplicate/retry behavior for repeated commands.
- Concurrency conflict behavior (if concurrency tokens are implemented).
- Boundary date/time handling in status changes and submission timestamps.

#### Preconditions
- Seed data for not-found, duplicate, and boundary-state cases.

#### Postconditions
- Service returns predictable error/result states.
- Persistence remains consistent with no unintended side effects.

---

## Fixture / Data Setup

### Core Fixture Pattern
1. Create and open a SQLite in-memory connection.
2. Build DbContext options using that open connection.
3. Ensure schema is created (or apply migrations if used by test project).
4. Seed only minimal baseline data required for each scenario.
5. Dispose context and connection at fixture/test end.

### Seed Builders (Recommended)
- `ApplicationSeedBuilder` for status-specific application records.
- `SponsorSeedBuilder` for sponsor combinations.
- `MemberSeedBuilder` for existing member-account collisions/uniqueness tests.

### Isolation Strategy
- Preferred: fresh in-memory database per test.
- Alternate: shared fixture + deterministic cleanup between tests.

---

## Priority Order (Execution)
1. P1: Submit flow persistence + default status.
2. P1: Status transition validity + status history.
3. P1: Accepted flow creates member account.
4. P2: Actionable queue query behavior.
5. P2: Member creation constraint/uniqueness failures.
6. P2: Not-found/duplicate/robustness scenarios.
7. P3: Boundary and extended regression cases.

## Notes
- Authorization concerns remain outside this class-library test plan unless service-level authorization is later introduced.
- Keep fixtures deterministic so assertions remain stable across runs.
