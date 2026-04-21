# Area D: Player Scoring — UI Analysis & Fluent UI Redesign

---

## Current UI Issues

### 1. RecordScore.razor (`/scores/record`)

**Scorecard layout**
- The scorecard renders as a traditional 21-column wide Bootstrap table (Hole 1–9 | Out | 10–18 | In | Total). The layout is functionally correct but collapses badly on anything narrower than a 1080 px desktop — the `table-responsive` wrapper produces a horizontal scrollbar rather than a redesigned mobile layout.
- There is no Stroke Index (SI) row. Members cannot see the difficulty ranking of each hole, which is essential for handicap-aware play.
- There is no Yardage row. The scorecard shows Par and Score only, giving the member no course-distance context.

**Par data — hardcoded and tee-unaware**
- The par array is a static C# field: `private static readonly int[] par = [4, 5, 3, 4, 4, 4, 4, 5, 4, 4, 4, 3, 5, 4, 4, 3, 5, 4];`
- It is defined in the component itself, not read from the database. It does not change when the member selects a different tee colour (White, Yellow, Red, etc.), even though par can legitimately differ by tee on some holes.
- The gap analysis notes that holes 7 and 17 require dual-par values for certain tee configurations; these are absent entirely.

**Tee colour selection**
- Rendered as plain Bootstrap radio buttons in a `<dl>` row. There is no visual swatch (colour indicator) beside each option, making the selection purely text-based. The default is `TeeColor.White`, set in code rather than derived from the member's registered playing category.

**Running totals**
- Out, In, and Total cells for the Score row are computed inline in the template using LINQ. They display `—` until all 9 (or 18) holes have a value, offering no progressive feedback during entry. A member who has entered 7 of 9 front holes cannot see their partial front-9 running total.

**Hole entry inputs**
- Each cell contains a plain `<input type="number">` with a `min=1 max=20` constraint. There is no visual indication (colour coding) of whether a hole score is better than, equal to, or worse than par.
- The `@onchange` binding (not `@bind`) means a value is committed only on blur, not on keystroke. Tabbing through all 18 cells should work, but the UX is silent about progress.

**Submission gate**
- The Submit button is disabled until all 18 values are present. The hint message ("All 18 hole scores are required") only appears when the button is disabled — it disappears on completion, leaving no final confirmation prompt before the irreversible submit.
- There is no back/cancel action; navigating away silently discards entered data without warning.

---

### 2. MyScoreSubmissions.razor (`/scores/my`)

**Eligible rounds section**
- Shows Date, Time, Players, and a "Record Score" link. Does not show the booking's tee time slot duration, any minimum-time status, or why a booking might not yet be eligible (e.g. "Round completes at 14:30 — eligible then"). A member seeing an empty list receives no explanation beyond a static alert.

**Past rounds section**
- Columns: Date, Tee, Total Score, Submitted On.
- No per-hole breakdown or drill-down link. A member cannot review which holes they scored poorly on.
- No front-9 / back-9 split visible.
- No score-to-par delta shown (e.g. "+5" or "77 (+5)").
- No round status (e.g. Pending Attestation, Approved, Voided) — there is no attestation workflow at all, so all rounds appear identically regardless of any downstream state.
- No way to dispute or request a correction for a submitted round.
- The Total Score is computed live via `context.Scores.Sum(...)` rather than read from a stored field, confirming the gap-analysis finding that no derived total is persisted.

---

### 3. ScoreConsole.razor (`/scores/staff`)

**Date scope — today only**
- The query hard-filters to `DateTime.Today` with no UI control to change the date. A clerk cannot look up yesterday's rounds, review a specific date, or check whether a round from two days ago was ever scored. The header text explicitly says "Today's tee times" with no date picker.

**Status values**
- Three statuses: "Scored ✓", "Eligible", "Time-lock". Status mapping uses raw string comparisons (`context.Status == "Scored ✓"`), which is fragile. The status "Time-lock" is displayed as plain text with only a secondary grey badge — no tooltip or explanation of when the lock will lift.

**Actions — read-only for scored rounds**
- Once a booking has status "Scored ✓" the Action column shows only a dash. There is no:
  - Link to view the submitted scorecard.
  - Edit or void capability.
  - Attestation / approval action.
- A clerk who needs to correct a wrongly entered score has no path forward from this UI.

**MinDuration duplication**
- The `MinDuration(int playerCount)` switch expression is duplicated verbatim from `ScoreService`. A change to the eligibility window in the service will not be reflected here unless both copies are updated simultaneously.

**No pagination, filtering, or sorting controls**
- For a busy day with many tee times, the QuickGrid renders all rows with no visible column-header sort UI beyond the `SortBy` definitions (QuickGrid renders sort indicators, but there is no filter input).

---

### 4. ScoreConfirmation.razor (`/scores/confirmation`)

**What is shown**
- Date, Time, Tee Colour, Total Score, Member (admin only), Submitted On — displayed in a narrow Bootstrap card (max-width 480 px). Essentially a receipt slip.

**What is missing**
- No per-hole scorecard summary; member cannot verify their own hole-by-hole entries were recorded correctly before leaving the page.
- No score-to-par summary (+/- relative to par).
- No front-9 / back-9 split.
- Total Score is computed from `round.Scores.Sum(...)` inline, consistent with the lack of a stored total.
- For admins, no "Record another" or "Return to Score Console" shortcut to the date they were working on — only a generic "Return to Score Console" link.

---

## Fluent UI Blazor Redesign Proposal

### Scorecard Entry (Member) — replaces RecordScore.razor

**Purpose:** Allow a member (or admin on behalf of a member) to enter 18 hole scores against an existing booking, with live visual feedback and an explicit submit confirmation.

**Recommended Fluent UI Blazor components**

| Component | Usage |
|---|---|
| `<FluentCard>` | Outer wrapper for Round Details and Scorecard sections |
| `<FluentSelect>` | Tee colour selector, each option rendered with an inline colour swatch via `<FluentIcon>` or a coloured `<span>` |
| `<FluentNumberField>` | Per-hole score entry; replaces `<input type="number">` — supports `Min`, `Max`, `Step`, and native keyboard navigation |
| `<FluentBadge>` | Score-vs-par indicator per hole (Eagle / Birdie / Par / Bogey / Double+) colour-coded via `Appearance` |
| `<FluentDialog>` | Pre-submit confirmation dialog ("Submit 18-hole round for [Name] — [Date]?") with Cancel / Confirm actions |
| `<FluentProgressRing>` | Loading state (replaces Bootstrap spinner) |
| `<FluentMessageBar>` | Error and warning messages (replaces `.alert-danger`) |

**Layout sketch**

```
┌─────────────────────────────────────────────────────────────────┐
│ FluentCard — Round Details                                      │
│  Date & Time: Saturday, April 19 2025, 9:30 AM                 │
│  Players: 3                                                     │
│  Tee Colour: [FluentSelect ▼]  ● White  ● Yellow  ● Red        │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ FluentCard — Scorecard                                          │
│                                                                 │
│  FRONT 9                                                        │
│  ┌──────┬────┬────┬────┬────┬────┬────┬────┬────┬────┬──────┐  │
│  │ Hole │  1 │  2 │  3 │  4 │  5 │  6 │  7 │  8 │  9 │  OUT │  │
│  ├──────┼────┼────┼────┼────┼────┼────┼────┼────┼────┼──────┤  │
│  │ Yds  │355 │510 │175 │390 │420 │400 │320 │480 │415 │ 3465 │  │
│  │ Par  │  4 │  5 │  3 │  4 │  4 │  4 │  4 │  5 │  4 │   37 │  │
│  │  SI  │  7 │ 13 │ 17 │  3 │  1 │  9 │ 15 │  5 │ 11 │      │  │
│  │Score │[n] │[n] │[n] │[n] │[n] │[n] │[n] │[n] │[n] │  —   │  │
│  │ ±Par │    │    │    │    │    │    │    │    │    │      │  │
│  └──────┴────┴────┴────┴────┴────┴────┴────┴────┴────┴──────┘  │
│                                                                 │
│  BACK 9  (identical structure, holes 10–18, IN total)          │
│                                                                 │
│  ┌─────────────────────────────────────────────┐               │
│  │  OUT: 39  │  IN: —  │  TOTAL: —             │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
│  [FluentButton Appearance="Accent"  disabled until all 18]     │
│    Submit Round                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Key design decisions**
- Par and Yardage rows populated from the database keyed to the selected `TeeColor`, not hardcoded. When the member changes tee colour the scorecard rows update reactively.
- SI row read from course data; dual-par holes (e.g. holes 7, 17) rendered with a split cell or tooltip.
- `<FluentNumberField>` supports `@bind-Value` (two-way), so the running total updates on every keystroke rather than on blur.
- Score vs par per hole: after a value is entered, the cell footer shows a `<FluentBadge>` — green for Eagle/Birdie, white for Par, amber for Bogey, red for Double-bogey-or-worse. This matches the appearance of a standard golf scorecard app.
- OUT and IN subtotals update progressively as holes are entered (not gated on all-9-complete).
- `<FluentDialog>` fires on Submit click, showing a full summary (name, date, tee, total) with Cancel / Confirm. This provides the missing confirmation step before an irreversible write.
- A "Discard and go back" `<FluentButton Appearance="Stealth">` is added beside Submit so the member has a safe exit path with an implicit are-you-sure guard.

---

### My Score History (Member) — replaces MyScoreSubmissions.razor

**Purpose:** Give members a clear view of rounds they can still score and a meaningful history of submitted rounds with score context.

**Recommended Fluent UI Blazor components**

| Component | Usage |
|---|---|
| `<FluentCard>` | Section wrapper for "Eligible to Score" and "Score History" |
| `<FluentDataGrid>` | Score history table with sortable columns, pagination |
| `<FluentBadge>` | Round status (e.g. Submitted, Pending Attestation, Approved, Voided) |
| `<FluentDialog>` | Drill-down hole-by-hole scorecard overlay when a history row is clicked |
| `<FluentProgressRing>` | Loading state |
| `<FluentMessageBar>` | Empty-state and error messages |
| `<FluentButton>` | "Record Score" CTA per eligible booking |

**Layout sketch**

```
┌─────────────────────────────────────────────────────────────────┐
│ FluentCard — Rounds Available to Score                          │
│                                                                 │
│  Date         Time      Players   Eligible From    Action      │
│  Apr 19 2025  9:30 AM   3         11:30 AM ✓       [Record]    │
│  Apr 20 2025  14:00     1         16:00 (in 2h)    Locked      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ FluentCard — Score History                                      │
│                                                                 │
│  FluentDataGrid (sortable, paginated)                          │
│  Date ↓       Tee     F9   B9   Total   vs Par  Status         │
│  Apr 15 2025  Yellow  39   40   79      +7      [Submitted]    │
│  Mar 22 2025  White   42   44   86      +14     [Submitted]    │
│                                                     [View →]   │
│                                                                 │
│  Click row → FluentDialog shows full hole-by-hole scorecard    │
└─────────────────────────────────────────────────────────────────┘
```

**Key design decisions**
- Eligible bookings show "Eligible From" — the computed time at which the minimum-duration lock lifts — so members understand why a booking is not yet available rather than seeing a silent empty state.
- Time-locked rows display the lift time and are visually distinct (muted row, `<FluentBadge Appearance="Neutral">`).
- Score History adds F9, B9, and vs-Par columns derived at render time (no stored total required immediately, but these are good candidates for a future stored `TotalScore` column per the gap analysis).
- Status badge uses `<FluentBadge>` with `Appearance` mapped to workflow state: Filled/Accent for Submitted, Filled/Success for Approved, Filled/Warning for Pending Attestation, Filled/Danger for Voided.
- Row click opens a `<FluentDialog>` with a read-only hole-by-hole scorecard so the member can verify their entries. This satisfies the missing drill-down that the current UI lacks entirely.
- "Request Correction" link inside the dialog dialog provides a paper-trail path (could link to a support/contact form or a dedicated correction-request workflow).

---

### Score Console (Clerk/Staff) — replaces ScoreConsole.razor

**Purpose:** Allow club staff to monitor scoring status across any date, enter scores on behalf of members, review submitted scorecards, and (in future) attest or void rounds.

**Recommended Fluent UI Blazor components**

| Component | Usage |
|---|---|
| `<FluentDatePicker>` | Date selector — replaces the hard-coded `DateTime.Today` filter |
| `<FluentDataGrid>` | Main tee-sheet grid with sorting and column resizing |
| `<FluentBadge>` | Status chips (Scored, Eligible, Time-lock) |
| `<FluentDialog>` | Scorecard detail overlay for any scored round; future: void/attest actions |
| `<FluentButton>` | "Record Score", "View Scorecard", future "Attest", "Void" per row |
| `<FluentProgressRing>` | Loading state |
| `<FluentMessageBar>` | Error messages, empty-state notices |
| `<FluentTooltip>` | Explains Time-lock: "Eligible at [time]" on hover |

**Layout sketch**

```
┌─────────────────────────────────────────────────────────────────┐
│ Score Console                                                   │
│                                                                 │
│  Date: [FluentDatePicker  Apr 21, 2026 ▼]   [Today]           │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ FluentDataGrid                                                  │
│                                                                 │
│  Time ↑    Member          Players  Status             Actions  │
│  08:00     Smith, J.       4        [Scored ✓]         [View]  │
│  08:10     Patel, A.       2        [Eligible]  [Record Score] │
│  08:20     Okonkwo, T.     3        [Time-lock ⓘ]       —      │
│  08:30     Larsson, M.     1        [Time-lock ⓘ]       —      │
│                                                                 │
│  ⓘ hover → FluentTooltip: "Eligible at 10:30 AM"              │
└─────────────────────────────────────────────────────────────────┘
```

**Key design decisions**
- `<FluentDatePicker>` replaces the hard-coded today filter. Default is today, but the clerk can navigate to any past date to review or backfill scores. This eliminates the inability to look up historical rounds.
- "View" button for scored rows opens a `<FluentDialog>` containing the same hole-by-hole scorecard table as used in RecordScore, but read-only. This satisfies the missing "view submitted scorecard" capability.
- Future attestation: add "Attest" / "Void" `<FluentButton>` variants inside the dialog, behind a confirmation step. The current UI has no hook for this at all.
- `<FluentTooltip>` on the Time-lock badge shows the computed eligible time, derived by calling `ScoreService.MinDuration` — the `MinDuration` switch expression should be removed from ScoreConsole and called exclusively through the service, eliminating the duplication.
- Status badge `Appearance` mapping: `Accent` (Eligible), `Success` (Scored), `Neutral` (Time-lock), future `Warning` (Pending Attestation), `Danger` (Voided).
- The grid should support multi-day data: adding a Date column that is hidden when viewing a single day allows the same component to power a future "all rounds" admin view.

---

### Score Confirmation — replaces ScoreConfirmation.razor

**Purpose:** Provide a receipt-style confirmation after submission that allows the member (or admin) to verify their hole-by-hole data before leaving.

**Recommended Fluent UI Blazor components**

| Component | Usage |
|---|---|
| `<FluentCard>` | Confirmation summary wrapper |
| `<FluentBadge Appearance="Success">` | "Round Confirmed" status indicator |
| Inline scorecard table | Same hole/par/SI/score/±par structure as RecordScore, read-only |
| `<FluentButton>` | Navigation back to appropriate list |
| `<FluentDivider>` | Separates summary from scorecard detail |

**Layout sketch**

```
┌─────────────────────────────────────────────────────────────────┐
│ FluentCard                                                      │
│  [FluentBadge Success] Round Confirmed                          │
│                                                                 │
│  Date:         Saturday, April 19 2025                         │
│  Tee Time:     9:30 AM                                         │
│  Tee Colour:   White                                            │
│  Member:       Jane Smith  (admin view only)                    │
│  Submitted:    Apr 19 2025, 13:45                              │
│                                                                 │
│  FluentDivider                                                  │
│                                                                 │
│  [Read-only scorecard: Hole | Par | SI | Yds | Score | ±Par]   │
│   1   4  7  355  5  +1                                         │
│   2   5  13 510  5   E                                         │
│   …                                                            │
│                                                                 │
│  OUT: 39 (+2)  |  IN: 40 (+3)  |  TOTAL: 79 (+5)              │
│                                                                 │
│  [Return to My Scores]  or  [Return to Score Console]          │
└─────────────────────────────────────────────────────────────────┘
```

**Key design decisions**
- Full hole-by-hole read-only scorecard is shown so the member can immediately spot an entry error and contact staff. The current page shows only the total, making verification impossible.
- Score vs par delta shown per hole and in the OUT / IN / Total footers.
- The member confirmation path and admin path use the same card layout — only the navigation button and the optional Member field differ.
- Admin-path "Return to Score Console" button should preserve the date the admin was viewing (pass the date as a query parameter) so the clerk is not dropped back to today's default when they may have been working on a historical date.

---

## Notes

1. **Par/SI/Yardage data source:** All three rows in the redesigned scorecard depend on course data stored in the database keyed to `TeeColor`. The hardcoded `par[]` array must be replaced by a `CourseHole` entity (or equivalent) with columns for each tee colour's par, yardage, and SI. This is a back-end prerequisite for the redesign; the UI work should not proceed independently.

2. **Stored total score:** The redesign continues computing totals in the UI for display purposes, but the gap analysis correctly identifies that `TotalScore` should be persisted on the `GolfRound` entity. Sorting and filtering history by total score — a natural UX need — becomes expensive or impossible without it.

3. **MinDuration encapsulation:** The `MinDuration` switch in `ScoreConsole.razor` (lines 170–176) is identical to the one in `ScoreService`. The console component should inject `ScoreService` and call the service method, removing the local copy. The redesigned `<FluentTooltip>` for Time-lock status depends on this same computation.

4. **Attestation workflow:** The redesign reserves action slots for Attest and Void in the Score Console dialog and a "Request Correction" path in the member history drill-down, but the back-end state machine (pending → attested → voided) does not yet exist. These UI hooks should be stubbed but disabled until the service layer is in place.

5. **Mobile scorecard:** The 21-column table does not fit a phone screen. For mobile-width viewports the scorecard should collapse into two separate `<FluentCard>` panels (Front 9, Back 9), each with 9 data columns plus a subtotal. This is achievable with CSS container queries or `@media` breakpoints around the two card variants.

6. **Tee colour swatch:** The `<FluentSelect>` option items for tee colour should include a small coloured circle (`background-color` inline style or a `<FluentIcon>` with a tee colour mapping) so the selection is visually unambiguous — particularly important for members with colour names that do not obviously map to physical tee markers on the course.
