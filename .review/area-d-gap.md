# Area D: Player Scoring — Gap Analysis

## Summary

The core round-submission pipeline (record score → persist → confirmation) is functional and well-tested for the club's own course. The primary gaps are structural: the `GolfRound` entity lacks the fields required by WHS (course rating, slope rating, course name, par-per-hole, computed total), there is no Handicap Index calculation or report, no support for external/away courses, and the clerk console is today-only with no editorial controls. The time-lock logic itself is sound and thoroughly tested.

---

## Missing Features

1. **Handicap Index Calculation and Report** — No handicap index is ever computed. The spec requires a report showing: Date, Member Name, Handicap Index, Last 20 Average, Best 8 Average, and Last 20 Round Scores. No such service, entity, or page exists anywhere in the codebase. **Severity: High**

2. **External / Away Course Support** — Members are required to be able to incorporate scores from Golf Canada–approved courses other than the home club. There is no mechanism to record a round for an external course (no entity, no UI, no service method). Rounds can only be submitted against a `TeeTimeBooking` at the club. **Severity: High**

3. **Course Rating and Slope Rating Not Stored** — `GolfRound` stores `SelectedTeeColor` but not the actual course rating or slope rating values. WHS differential calculation (`(Score − Course Rating) × 113 / Slope Rating`) is impossible without these values. The rating lookup table exists nowhere in code. **Severity: High**

4. **Golf Course Name Not Stored per Round** — The spec requires "Golf Course" to be recorded per round. For home rounds the course is implicit, but there is no field for it on `GolfRound`. For external rounds it is mandatory. **Severity: Medium**

5. **Per-Round Par Data Not Stored** — Par values are hardcoded as a static array in `RecordScore.razor` and are never persisted. If the course configuration changes, historical differential calculations will silently use wrong par values. **Severity: Medium**

6. **Total Score Not Persisted** — Total is computed on the fly by summing `Scores` in the UI. There is no stored `TotalScore` field on `GolfRound`. WHS history queries and the Handicap Index report require a reliable total. **Severity: Low** (derivable, but inconvenient and error-prone at query time)

7. **Clerk Cannot Edit or Void a Submitted Round** — `ScoreConsole` lets a clerk record a new score (via the "Eligible" action) but provides no way to correct or void an already-submitted round. The spec says scores are "processed by a CLERK"; correction capability is an implied requirement. **Severity: Medium**

8. **No Scorecard Report Available to Members or Clerks** — There is no page that lets a member or clerk view the full hole-by-hole scorecard for a past round. `MyScoreSubmissions` shows only Total Score and Tee; `ScoreConfirmation` shows only the aggregate total. **Severity: Medium**

9. **Hole 7 and Hole 17 Dual-Par Not Implemented** — The spec states Hole 7 par is 3/4 and Hole 17 par is 4/5 (two options depending on tee or context). The hardcoded par array in `RecordScore.razor` uses a single value per hole with no gender/tee distinction (`[4, 5, 3, 4, 4, 4, 4, 5, 4, 4, 4, 3, 5, 4, 4, 3, 5, 4]`). This means the displayed par is approximate and gender-unaware. **Severity: Medium**

10. **Stroke Index Not Displayed** — The spec defines Stroke Index (SI) for Men and Women. The scorecard UI (`RecordScore.razor`) shows only Hole and Par rows; no SI row is present. **Severity: Low**

11. **Gender-Specific Tee Ratings Not Stored or Validated** — The spec provides distinct Course/Slope ratings for Men and Women on each tee (e.g., Red Men 66.2/116, Women 71.0/125). `GolfRound` stores only `TeeColor`, not gender, so even if ratings were computed they would be wrong for roughly half of all submissions. **Severity: High** (if/when WHS calculation is added)

---

## WHS Compliance Analysis

| WHS Requirement | Implemented? | Notes |
|---|---|---|
| Date of round | Partial | Derived from `TeeTimeBooking.TeeTimeSlotStart`; not stored directly on `GolfRound` |
| Golf Course name | No | Implicit for home rounds; no field exists |
| Course Rating | No | Not stored on `GolfRound` |
| Slope Rating | No | Not stored on `GolfRound` |
| Hole-by-hole scores | Yes | `Scores` JSON column, 18 elements |
| Total Score | Derived only | Summed at query time; no stored field |
| Score Differential calculation | No | Requires Course Rating + Slope Rating |
| Best 8 of last 20 averaging | No | No calculation logic exists |
| Handicap Index computation | No | No service or entity for this |
| Handicap Index report | No | No page or data exists |
| External course scores | No | Architecture only supports club bookings |

---

## Data Model Analysis

`GolfRound` entity (`ClubBaist.Domain2/Entities/Scoring/GolfRound.cs`):

**Present:**
- `Id` (PK), `TeeTimeBookingId` (FK), `MembershipId` (FK), `SelectedTeeColor` (enum), `Scores` (JSON `List<uint?>`), `SubmittedAt`, `ActingUserId`
- Unique composite index on `(TeeTimeBookingId, MembershipId)`

**Missing:**
- `CourseRating` (decimal) — required for WHS differential
- `SlopeRating` (int) — required for WHS differential
- `CourseName` (string) — required by spec
- `TotalScore` (int or computed) — spec and report require it
- `IsExternalRound` (bool) + external course FK or free-text fields — for away rounds
- `Gender` or derived flag — needed for rating lookup (Men vs Women on same tee)
- Par-per-hole snapshot — currently hardcoded in UI, not persisted

The unique index correctly enforces one round per booking per member, but it does not accommodate the external-round use case at all, since external rounds have no `TeeTimeBookingId`.

---

## Time-Lock Logic Analysis

The `MinDuration` function is duplicated in two places: `ScoreService.cs` and `ScoreConsole.razor`. Both copies are identical:

```
1 player  → 2 h
2 players → 2.5 h
3 players → 3 h
4+ players → 3.5 h
```

This duplication is a maintenance risk — if the business changes the thresholds, both must be updated. The logic should live only in `ScoreService` and be called from `ScoreConsole`.

The logic itself is reasonable and matches the participant-count-based model. There is no specification document that defines what these exact thresholds should be, so they cannot be called incorrect, only noted as unverified against the business requirement.

The service correctly enforces the time-lock on both the eligibility query (`GetEligibleBookingsAsync`) and the submit path (`SubmitRoundAsync`), preventing bypass via direct POST.

---

## Clerk/Staff Workflow Analysis

`ScoreConsole.razor` (`/scores/staff`):

- Restricted to `Admin` role — correct.
- Shows **today's tee times only**. The spec says scores are processed after a round is completed; a clerk working the next morning cannot access yesterday's unscored rounds from this screen.
- Displays Time, Member Name, Players, Status (Scored / Eligible / Time-lock), and an action link.
- The action link navigates to `/scores/record` — the same page members use. This is functional but conflates clerk and member workflows.
- **No search or filter**: if there are many tee times, there is no way to filter by member name or status.
- **No refresh button**: the page loads once; a clerk who submits a score and returns sees stale status until they manually reload.
- **No void/correction capability**: once a round is marked "Scored ✓", there is no clerk action available.
- The "report made available" post-processing step mentioned in the spec (presumably the Handicap Index report) does not exist.

---

## UI Issues

1. `RecordScore.razor` hardcodes a single par array regardless of tee color or gender, giving an incorrect par display for Red and Blue tee players.
2. `MyScoreSubmissions.razor` is restricted to `AppRoles.Member` only; admins cannot access their own score submissions via this page (they must use the staff console, which only shows today).
3. `ScoreConfirmation.razor` shows "No submitted round found" as a warning with a "Return to My Score Submissions" link even when the actor is an admin — the fallback link is wrong for admins.
4. No navigation link to `MyScoreSubmissions` or `ScoreConsole` exists in the pages themselves; they rely on the site nav structure (not reviewed).
5. The tee-color radio buttons in `RecordScore.razor` default to White regardless of member gender, which may be inappropriate (Red tees are traditionally Women's tees at many clubs).

---

## Validation Gaps

1. **No maximum-score-relative-to-par cap** — WHS requires a maximum hole score of Net Double Bogey (par + 2 + handicap strokes). The system accepts any value 1–20, which may inflate differentials.
2. **No check that the member's gender is compatible with the selected tee** — a male member could submit on Red tees with male ratings (which don't exist in the system), or vice versa.
3. **No future-date guard on `SubmittedAt`** — the time-lock prevents too-early submission, but `SubmittedAt` is set from `DateTime.Now` server-side, so this is not a client exploit vector. Still, there is no assertion in the service that `SubmittedAt >= booking.TeeTimeSlotStart`.
4. **Score zero rejected client-side only** — The `<input min="1">` attribute prevents zero in modern browsers, but the service-side validation also covers it (`scores[i]!.Value < 1`). This is correctly layered.
5. **No validation that `BookingId` query parameter is non-zero in `RecordScore.razor`** — a GET to `/scores/record?bookingId=0` reaches `OnInitializedAsync`, queries for `Id == 0`, gets null, and shows "Booking not found" rather than a proper 400/redirect.

---

## Test Coverage Gaps

`ScoreServiceTests.cs` contains 30 tests (T01–T30) covering:
- Eligibility (T01–T10): no-bookings, time-lock boundaries for 1–4 players, already-scored exclusion, cross-member isolation, additional-participant exclusion
- Submission happy path (T11–T14): persist, `SubmittedAt` kind, `ActingUserId`, post-submit ineligibility
- Submission validation (T15–T25): unknown member, wrong owner, time-lock, duplicate sequential, wrong count, null/zero/21 scores, boundary values 1 and 20
- Concurrency (T26): sequential scopes simulating race; T27: `SaveChanges` throws
- GetRoundsByMember (T28–T30): empty, ordering, cross-member isolation

**Not tested:**
- Handicap Index calculation (no implementation to test)
- External course round submission
- `GetRoundsByMemberAsync` with more than 20 rounds (WHS "last 20" boundary)
- `RecordScore.razor` component behaviour (no Blazor component tests exist)
- `ScoreConsole.razor` component behaviour (same)
- Admin submitting a score on behalf of a member vs. member submitting themselves (different `ActingUserId` paths)
- `SubmitRoundAsync` with a non-Admin `actingUserId` that does not match the member's own user ID (no role check in service)

---

## Notes

- The par array hardcoded in `RecordScore.razor` (`[4, 5, 3, 4, 4, 4, 4, 5, 4, 4, 4, 3, 5, 4, 4, 3, 5, 4]`) sums to 71 (front 9: 37, back 9: 34). This does not match any of the rated pars implied by the course ratings in the spec. The spec's course ratings suggest a par around 70–72; this would need verification against the actual scorecard.
- `IsolationLevel.Snapshot` is used for the submit transaction, which is appropriate for optimistic concurrency on SQL Server, but Snapshot Isolation must be explicitly enabled on the database (`ALTER DATABASE ... SET ALLOW_SNAPSHOT_ISOLATION ON`). There is no migration or startup check confirming this is configured.
- The `ThrowingOnSaveDbContext` test double in `ScoreServiceTests` re-exposes internal `AppDbContext` sets directly, which tightly couples tests to the concrete EF context. If `IAppDbContext2` grows new members, the fake will break at compile time — this is acceptable given it is test-only code.
