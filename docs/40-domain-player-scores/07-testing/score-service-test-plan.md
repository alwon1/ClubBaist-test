# ScoreService – Test Plan

High-level test case descriptions for UC-PS-01. These are not code — they describe the intent and expected outcome of each test. Implementation follows the existing MSTest + EF in-memory DB pattern in `ClubBaist.Domain2.Tests`.

---

## E1 — GetEligibleBookingsAsync

Tests the time-lock filter and the "already scored" exclusion.

| ID | Scenario | Expected outcome |
|----|----------|-----------------|
| T-01 | Member has no past bookings | Empty list returned |
| T-02 | Member has one booking; tee time was 1 hour ago (1-player, lock = 2 h) | Empty list — inside time-lock window |
| T-03 | Member has one booking; tee time was exactly 2 hours ago (1-player) | Booking included — boundary: lock just elapsed |
| T-04 | Member has one booking past the lock; a `GolfRound` already exists for it | Empty list — already scored |
| T-05 | Member has two eligible bookings and one inside the lock window | Only the two eligible bookings returned |
| T-06 | 2-player booking, exactly 2 h 30 m elapsed | Included — boundary |
| T-07 | 3-player booking, exactly 3 h elapsed | Included — boundary |
| T-08 | 4-player booking, exactly 3 h 30 m elapsed | Included — boundary |
| T-09 | Member is an additional participant on a booking (not the primary booker) | That booking does not appear — only primary-booker bookings are eligible |
| T-10 | Multiple members each have eligible bookings | Only the queried member's bookings returned — no cross-member leakage |

---

## E2 — SubmitRoundAsync — Happy Path

| ID | Scenario | Expected outcome |
|----|----------|-----------------|
| T-11 | Valid request: active member, eligible booking, 18 scores all 1–20 | `GolfRound` stored; `ScoreSubmissionResult.Success = true` |
| T-12 | Submitted `GolfRound.SubmittedAt` | Set to server-side UTC — not the value from any client field |
| T-13 | Submitted `GolfRound.ActingUserId` | Matches `actingUserId` parameter; not a client-supplied value |
| T-14 | After successful submission, booking no longer appears in `GetEligibleBookingsAsync` | Confirmed — `GolfRound` exists for booking |

---

## E3 — SubmitRoundAsync — Validation Failures

Each test expects `ScoreSubmissionResult.Success = false` and no `GolfRound` stored.

| ID | Scenario | Expected error |
|----|----------|---------------|
| T-15 | `BookingMemberId` does not match any `MemberShipInfo` record | Member not found / not active |
| T-16 | Booking exists but `BookingMemberId` on the request does not match `TeeTimeBooking.BookingMemberId` | Booking not owned by member |
| T-17 | Booking tee time is 1 hour ago (1-player, lock = 2 h) — inside window | Round not yet eligible |
| T-18 | `GolfRound` already exists for the booking (sequential duplicate) | Score already submitted |
| T-19 | `Scores` list has 17 elements | Incomplete scorecard |
| T-20 | `Scores` list has 19 elements | Incomplete scorecard |
| T-21 | One score is `null` | Incomplete scorecard |
| T-22 | One score is `0` | Score out of range |
| T-23 | One score is `21` | Score out of range |
| T-24 | All 18 scores are `20` (boundary — maximum valid) | Success — `GolfRound` stored |
| T-25 | All 18 scores are `1` (boundary — minimum valid) | Success — `GolfRound` stored |

---

## E4 — SubmitRoundAsync — Concurrency

| ID | Scenario | Expected outcome |
|----|----------|-----------------|
| T-26 | Two simultaneous submissions for the same booking; first commits successfully | Second transaction hits the unique index on `TeeTimeBookingId`; `DbUpdateException` caught; returns `ScoreSubmissionResult(false, "Score already submitted for this round")` |
| T-27 | Score service fails (simulated `SaveChangesAsync` exception) | Transaction rolled back; no `GolfRound` stored; `ScoreSubmissionResult.Success = false` |

---

## E5 — GetRoundsByMemberAsync

| ID | Scenario | Expected outcome |
|----|----------|-----------------|
| T-28 | Member has no submitted rounds | Empty list returned |
| T-29 | Member has two submitted rounds | Both returned, ordered by `SubmittedAt` descending |
| T-30 | Two members each have rounds | Only the queried member's rounds returned — no cross-member leakage |

---

## Infrastructure Notes

- Use the EF Core in-memory database provider (already configured in `TestInfrastructure.cs`).
- Seed `TeeTimeBooking`, `MemberShipInfo`, and `TeeTimeSlot` records as needed per test — follow the existing patterns in `ServiceBehaviorTests.cs`.
- Time-lock boundary tests (T-03, T-06–T-08) require injecting a controllable `DateTime` source into `ScoreService` rather than calling `DateTime.UtcNow` directly. This is the only infrastructure requirement beyond the existing test setup.
- Concurrency test (T-26) can be simulated by inserting a `GolfRound` directly via `db` between the service's pre-check and its `SaveChangesAsync` call, or by running two tasks against the same in-memory context.
