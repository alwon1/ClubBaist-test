# Area D: Player Scoring — Prioritized Task List

---

## Critical (correctness bugs / MinDuration duplication)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| C1 | Remove duplicate `MinDuration` switch from `ScoreConsole.razor`; call `ScoreService.MinDuration` (or a `static ScoringRules` helper in the Domain project) exclusively | H | XS | One-file change; zero risk of regression — both copies are already identical. Must be done before any UI redesign of ScoreConsole touches that branch. |
| C2 | Fix `ScoreConsole.razor` status comparisons — replace raw string literals (`"Scored ✓"`, `"Eligible"`, `"Time-lock"`) with an enum or named constants | H | XS | Fragile string matching is a silent correctness risk; will break if display text ever changes. |
| C3 | Add a `bookingId=0` guard in `RecordScore.razor` `OnInitializedAsync` — redirect or return 400 when the query parameter is zero or missing | M | XS | Currently reaches the DB with `Id == 0` and silently shows "Booking not found" instead of a proper error state. |
| C4 | Fix `ScoreConfirmation.razor` admin fallback — "Return to My Score Submissions" link is shown to admins; replace with a role-aware "Return to Score Console" link | M | XS | Incorrect navigation for the admin path; one conditional in the template. |
| C5 | Confirm (or enable) Snapshot Isolation on the database — add a startup assertion or EF migration check that `ALLOW_SNAPSHOT_ISOLATION` is ON for the database | H | S | `IsolationLevel.Snapshot` in `SubmitRoundAsync` silently falls back or throws on a database where this is not configured; could cause data-integrity failures in production. |

---

## High Priority (WHS compliance / spec gaps)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| H1 | Create `Course` entity and `CourseRating` entity (CR/SR per tee colour per gender) with EF migration; seed home-club data from spec | H | M | Foundational dependency: H2, H3, H4, H5, and all scorecard UI work block on this. See design-report proposed schema. |
| H2 | Make `GolfRound.TeeTimeBookingId` nullable (`int?`); update unique index to a filtered unique index (`WHERE TeeTimeBookingId IS NOT NULL`); add `CourseId` FK; migrate | H | S | Prerequisite for external-course rounds. Must follow H1. Cascades to service validation logic. |
| H3 | Add `CourseRatingUsed` (`decimal(4,1)`) and `SlopeRatingUsed` (`int`) fields to `GolfRound`; populate from `CourseRating` lookup at submission time (denormalised) | H | S | WHS differential is impossible without these. Denormalise at write time so historical records survive future rating updates. Depends on H1 + H2. |
| H4 | Add `Gender` field to `GolfRound`; derive from the submitting member's profile at submission time; use to select the correct `CourseRating` row | H | S | Without gender, CR/SR lookup is ambiguous for every submission (Men and Women have distinct ratings on the same tee). Depends on H1 + H2 + H3. |
| H5 | Add `TotalScore` (stored `int`) to `GolfRound`; compute from `Scores` array in `SubmitRoundAsync` before `SaveChangesAsync` | H | XS | Required for WHS history queries and future Handicap Index report; eliminates fragile inline LINQ sums at query time. Can be done independently of H1–H4 but should be batched with the migration. |
| H6 | Extend `ScoreConsole.razor` date filter — add a `<FluentDatePicker>` (or equivalent) replacing the hard-coded `DateTime.Today` so clerks can access previous days' rounds | H | S | A clerk working the morning after cannot access unscored rounds at all today. This is a critical operational gap independent of any model changes. |
| H7 | Add `CourseHole` records (HoleNumber, Par, StrokeIndexMen, StrokeIndexWomen, Yardage) to the `Course` entity and replace the hardcoded `par[]` array in `RecordScore.razor` with a DB-driven lookup keyed to `TeeColor` | H | M | Prerequisite for correct par display on dual-par holes (7, 17) and for all scorecard redesign work. Depends on H1. |
| H8 | Restrict `MyScoreSubmissions.razor` to members AND admins (remove the `Member`-only role guard); ensure admins can view their own score history | M | XS | Admins currently have no self-service history view; staff console only shows today. |

---

## Medium Priority (design improvements)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| M1 | Add Stroke Index (SI) row to `RecordScore.razor` scorecard table | M | S | Spec-required; data source is `CourseHole` (depends on H7). Do not implement with hardcoded SI — wait for H7 or implement together. |
| M2 | Replace `RecordScore.razor` tee-colour radio buttons with visually colour-coded selectors; default selection to the member's registered playing category (gender-appropriate tee) | M | S | Currently defaults to White regardless of gender; Red tees are Women's tees at this club. Depends on H4 (gender on round) and H7 (tee-aware par). |
| M3 | Add progressive running totals (OUT partial, IN partial) to `RecordScore.razor` — update on every score entry rather than only when all 9 holes are complete | M | S | Low-risk UI-only change; improves usability significantly during entry. Independent of model changes. |
| M4 | Add pre-submit `<FluentDialog>` confirmation to `RecordScore.razor` and a "Discard and go back" cancel path | M | S | Submission is currently irreversible with no confirmation step and no safe exit. UI-only change; independent. |
| M5 | Add scorecard drill-down to `MyScoreSubmissions.razor` — click a past round to open a read-only hole-by-hole view (score vs par per hole, F9/B9 subtotals, vs-par delta) | M | M | Members currently cannot verify their own entries after submission. Requires `TotalScore` stored (H5) and `CourseHole` data (H7) for the par column. |
| M6 | Add score-vs-par colour coding per hole cell in `RecordScore.razor` (Eagle/Birdie = green, Par = neutral, Bogey = amber, Double+ = red) | M | S | Standard scorecard UX; requires H7 for accurate par per hole. |
| M7 | Add "View submitted scorecard" action to `ScoreConsole.razor` for Scored rows — open a read-only hole-by-hole overlay | M | M | Clerks currently have no way to see what was entered; prerequisite for any future void/correction capability. Depends on H6 (date picker) and H5 (stored total). |
| M8 | Add mobile-responsive layout to scorecard — collapse the 21-column table into two separate Front-9 / Back-9 panels at small viewports | M | M | Current horizontal scroll is unusable on mobile; `@media` breakpoints or container queries around two card variants. Depends on H7 for data-driven rows. |
| M9 | Add "Eligible From" computed time to `MyScoreSubmissions.razor` eligible-bookings list (show the time the minimum-duration lock lifts rather than silent empty state) | M | S | Members currently receive no explanation of why a booking is not yet scoreable. Calls `ScoreService.MinDuration` — after C1 is done this is a single-source call. |
| M10 | Add per-round par-snapshot storage — persist the par-per-hole array used at submission time on `GolfRound` (or via FK to `CourseHole`) so historical differential calculations remain correct if course configuration changes | M | M | Currently par is hardcoded in the UI and never stored; a course change silently corrupts historical records. Depends on H7. |
| M11 | Upgrade `Scores` JSON storage to EF Core 8+ native primitive collection mapping — remove the custom `ValueConverter` + `ValueComparer` boilerplate | L | S | Simplification only; requires a migration round-trip test to confirm existing data survives. Low risk but low urgency; do with next migration that touches `GolfRound`. |
| M12 | Enforce `GolfRound(TeeTimeBookingId, MembershipId)` unique constraint in `OnModelCreating` as a filtered unique index (rather than relying solely on application logic) | M | S | Currently enforced only by application code; a direct DB insert bypasses it. Should be added alongside H2. |
| M13 | Add a `<FluentTooltip>` on Time-lock status badges in `ScoreConsole.razor` showing the computed "Eligible at HH:MM" time | L | XS | Depends on C1 (MinDuration single source). Small UX improvement for clerks. |

---

## Low Priority (future / handicap system)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| L1 | Design and implement `HandicapRecord` entity — computed handicap index per member per calculation date, storing 20-round window and best-8 average | H | XL | Explicitly future-phase per north-star note. Blocked on H1–H5 being complete. Do not start until WHS round record is stable. |
| L2 | Implement Handicap Index calculation service — best 8 of last 20 differentials, using `(AdjustedGrossScore − CourseRatingUsed) × 113 / SlopeRatingUsed` | H | XL | Depends on L1, H3, H4, and `AdjustedGrossScore` being stored on `GolfRound`. Future phase. |
| L3 | Add Handicap Index report page — Date, Member Name, Handicap Index, Last 20 Average, Best 8 Average, Last 20 Round Scores | H | L | Depends on L1 + L2. Future phase. |
| L4 | Implement external / away course round submission — UI flow and service path for rounds with `TeeTimeBookingId = null` and a free-text or FK external course | H | XL | Depends on H1 + H2. Full new workflow (no booking to validate against; service validation path must branch). Future phase after home-round model is solid. |
| L5 | Add Net Double Bogey cap enforcement (WHS maximum hole score = par + 2 + handicap strokes) to `SubmitRoundAsync` | M | M | Requires handicap strokes per hole, which requires a Handicap Index (L1/L2). Cannot be meaningfully implemented until the handicap system exists. |
| L6 | Add gender-tee compatibility validation — reject submission where selected tee has no rating for the member's gender | M | S | Depends on H4 (gender on round) and H1 (CourseRating entity). Can be added once H1–H4 are complete. |
| L7 | Add `AdjustedGrossScore` field to `GolfRound` and compute ESC adjustment at submission time | M | M | WHS uses adjusted score, not raw total. Requires Net Double Bogey cap logic (L5 dependency). Future phase. |
| L8 | Implement clerk attestation workflow — `Submitted → Attested` state transition; `AttestRoundAsync` service method; Attest / Void actions in `ScoreConsole` dialog | M | L | Implied by the spec ("processed by a clerk"). Back-end state machine does not exist. Stub UI hooks in console redesign (M7) but disable until service layer is ready. |
| L9 | Add test coverage for `GetRoundsByMemberAsync` with > 20 rounds (WHS "last 20" boundary); admin-vs-member `ActingUserId` paths; `RecordScore.razor` and `ScoreConsole.razor` component tests | M | M | Currently no Blazor component tests exist for scoring pages. Add alongside or after any component redesign work. |
| L10 | Verify par array sum (currently 71; spec course ratings imply par 70–72) and dual-par holes 7 and 17 against the actual physical scorecard | M | XS | Data-verification task requiring access to the course scorecard document. Prerequisite to seeding H7 data correctly. |

---

## Grouped Tasks (must go together)

### Group 1 — Course Entity Foundation (H1 + H2 + M12)
`Course` and `CourseRating` entities, `GolfRound.TeeTimeBookingId` made nullable, `CourseId` FK added, filtered unique index applied — all in a single EF migration. These three changes touch the same migration and the same `GolfRound` entity configuration; splitting them creates unnecessary intermediate states.

### Group 2 — WHS Round Record Fields (H3 + H4 + H5)
`CourseRatingUsed`, `SlopeRatingUsed`, `Gender`, and `TotalScore` added to `GolfRound` in one migration, with `SubmitRoundAsync` updated to populate all four at write time. They share a single service-layer change and a single migration; doing them one-at-a-time wastes migration history. **Depends on Group 1.**

### Group 3 — Course Hole Data + Scorecard UI (H7 + M1 + M2 + M6 + M8 + M10)
`CourseHole` records (Par, SI, Yardage per tee) replace the hardcoded `par[]` array. All scorecard UI improvements — SI row, tee-aware par, score-vs-par colour coding, mobile layout, par-snapshot storage — depend on this data being available from the DB. These should be implemented as a single coordinated front-end + back-end sprint. **Depends on Group 1.**

### Group 4 — Score Console Operational Fixes (C1 + C2 + H6 + M7 + M13)
`MinDuration` deduplication, string-literal status constants, date picker, "View scorecard" overlay, and Time-lock tooltip are all changes to `ScoreConsole.razor` (and its backing service query). Shipping them together avoids multiple partial-state deploys of the same component and lets C1 immediately power M13's tooltip.

### Group 5 — Member Submission UX (M3 + M4 + M9)
Progressive running totals, pre-submit confirmation dialog, and "Eligible From" timing in `MyScoreSubmissions` are all member-facing UX improvements with no model dependencies (they work against the current data model). They can ship together as a single UX sprint before the model work lands.

### Group 6 — Handicap System (L1 + L2 + L3 + L7)
`HandicapRecord` entity, differential calculation service, ESC adjustment, and the Handicap Index report page form the complete future-phase feature. None of these should be started until Groups 1 and 2 are deployed and stable.

### Group 7 — External Course Support (L4 + L6)
External round submission and gender-tee compatibility validation both require Group 1 to be in place. They also need the Group 2 fields populated at write time. Implement as a coordinated sprint after Groups 1 and 2 are stable.

---

## Independent Tasks (can be parallelized)

The following tasks have no dependencies on each other or on the grouped tracks above and can be assigned and worked in parallel:

| Task | Parallelizable with |
|---|---|
| C1 — Remove `MinDuration` duplication from ScoreConsole | C2, C3, C4, C5, H8 |
| C2 — Replace raw status strings with constants/enum | C1, C3, C4, C5, H8 |
| C3 — Guard `bookingId=0` in RecordScore | C1, C2, C4, C5, H8 |
| C4 — Fix admin fallback link in ScoreConfirmation | C1, C2, C3, C5, H8 |
| C5 — Snapshot Isolation startup check | C1, C2, C3, C4, H8 |
| H8 — Allow admins to access MyScoreSubmissions | C1–C5 |
| H5 — Add stored `TotalScore` to GolfRound | Can be batched with Group 2 migration or shipped alone as a patch migration |
| Group 5 (M3 + M4 + M9) — Member UX sprint | Can run in parallel with Group 1 back-end work since it uses only the existing data model |
| L10 — Verify par array and dual-par holes against physical scorecard | Purely a data-verification task; can be done by a non-developer and should unblock Group 3 seeding |

---

## Dependency Order Summary

```
C1, C2, C3, C4, C5, H8   ←  no dependencies; do now
Group 5 (M3, M4, M9)     ←  no model dependencies; parallelisable with Group 1

Group 1 (H1, H2, M12)    ←  foundational; all WHS work blocks here
  └─ Group 2 (H3, H4, H5) ←  depends on Group 1
       └─ Group 7 (L4, L6) ←  external courses; future, after Group 2

Group 3 (H7 + scorecard UI) ← depends on Group 1; parallelisable with Group 2

Group 4 (C1→H6, M7, M13)  ← C1 is a prerequisite; rest depend on H6 being in place

Group 6 (L1, L2, L3, L7)  ← future phase; depends on Groups 1 + 2 being stable
L8 (attestation)           ← future; M7 stubs the UI hook; service layer TBD
L9 (component tests)       ← add alongside each component redesign sprint
```
