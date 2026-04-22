# Area C: Standing Tee Times — UI Analysis & Fluent UI Redesign

## Current UI Issues

### 1. StandingRequest.razor — Member Submission Form

**Tolerance Dropdown Exceeds Spec**
The tolerance `<select>` offers four options: 0, 15, 30, and 60 minutes. The business spec caps tolerance at ±30 min. The `±60 min` option must be removed; its presence lets members submit requests the service layer should reject, causing a confusing post-submit error rather than clear inline prevention.

**Foursome Selection — No Real-Time Feedback**
Three plain `<select>` dropdowns are used to pick additional players. The list is populated from `allMembers` — the full club roster — with no search or filter. Issues:

- Duplicate selection is not prevented in the UI. The submit handler catches it (`validPlayerIds.Distinct()` reduces the count to fewer than 3 and shows an error), but there is no immediate visual indication that two dropdowns have the same person selected.
- No confirmation of who has been selected is shown until after submission.
- The member query at line 181 uses `.OrderBy(m => m.User.LastName)` but the `User` navigation property is not guaranteed to be loaded (the same `GetAllAsync` NPE risk noted in the gap analysis applies here on first load if EF lazy-loading is not configured).
- With a large membership list the flat `<select>` is unwieldy; no typeahead or search is available.

**Date Range Pickers — No Validation Before Submission**
`startDate` and `endDate` are plain `<input type="date">` controls. The following checks are entirely absent in the UI:

- No guard that `endDate >= startDate`.
- No guard that `startDate >= today` (a member can submit a request for a date already in the past).
- The default `endDate` is six months out (`DateTime.Today.AddMonths(6)`), which may be longer than any season the club defines; there is no maximum enforced.

All validation happens only in `SubmitRequest()`, so the member does not learn of an invalid range until they click Submit.

**Time Picker — No Min/Max or Step**
`<input type="time">` has no `min`, `max`, or `step` attributes. A member can enter any time including midnight or 01:30. Typical club tee-sheet windows (e.g. 06:00–18:00) are not enforced.

**No Foursome Validation Feedback**
As noted in the gap analysis, there is no inline feedback for the foursome constraint. The only indication that fewer than 3 players were selected (or that duplicates were picked) is a top-of-page `alert-danger` after Submit is clicked. There is no count, no per-slot error, and no visual distinction between a filled and an empty slot.

---

### 2. MyStandingRequests.razor — Member History View

**Single Aggregate Status Only**
The `QuickGrid` shows one status badge per request (e.g. `Approved`, `Allocated`, `Unallocated`). There is no per-week history — a member cannot see which individual Saturdays were actually allocated versus missed, nor can they see the pattern of allocations over the season.

**ApprovedBy / ApprovedDate Not Shown**
These fields are missing from both the entity (per gap analysis) and from this view. The member sees an approved time and a priority number but not who approved the request or when, which limits auditability and member confidence.

**Navigation Property NPE Risk**
The `Players` column (line 61) accesses `context.BookingMember.User.FirstName` and `p.User.FirstName` on every row. Because `GetForMemberAsync` is backed by a service that uses `GetAllAsync` with no `Include`, these navigation properties may be null. This would throw a `NullReferenceException` at render time, crashing the page silently or surfacing a generic error.

**Cancel Confirm UX**
The inline "Are you sure? Yes / No" confirmation replaces the Cancel button within the same table cell. In a narrow grid column this is visually cramped and the cell width shifts when the confirmation text appears, causing row re-layout jitter.

**No Empty-Slot Indicator for Tolerance**
The `±@context.ToleranceMinutes min` label can display `±60 min` for requests submitted before the spec was corrected, silently surfacing invalid historical data to the member with no flag.

---

### 3. StandingTeeTimes.razor — Admin/Staff Management

**Approve Panel Time Defaults to 08:00, Not Member's Requested Time**
`OpenApprovePanel` (line 220–226) always resets `approvedTimeStr = "08:00"`. This means an admin approving a request for 09:30 must manually overwrite the field. The member's `RequestedTime` is available on the entity in scope (`context.RequestedTime`) but is never used to pre-populate the input. This is a high-friction workflow error and a direct cause of incorrect approved times being committed.

**Priority Number — No Conflict Detection**
The priority number input is a plain `<input type="number">` with `min="1"` and no upper bound. There is no check that the entered priority number is not already assigned to another active request. An admin can assign priority #1 to two different requests and the UI will accept both.

**No ApprovedBy / ApprovedDate Capture**
The `ApproveAsync` call (line 251) passes only `approvedTime` and `approvedPriority`. There is no mechanism to record which admin performed the approval or when. These fields are absent from the entity and therefore cannot be surfaced in either the admin grid or the member view.

**Actions Only for Draft Status**
The Actions column is entirely empty for any request that is not `Draft` (line 112: `@if (context.Status == StandingTeeTimeStatus.Draft)`). There is no ability to revert an accidental approval, re-open a denied request, or manually force a status change. There is also no deny-reason field captured.

**Filter Buttons — No URL State**
Status filter state is held in `statusFilter` (an in-memory field). Navigating away and returning resets the filter to All. Deep-linking to, for example, all `Draft` requests is not possible.

**GetAllAsync NPE Risk**
`allRequests = await StandingTeeTimeService.GetAllAsync()` with no Include means the `BookingMember.User` and `AdditionalParticipants[*].User` navigation properties accessed in every `TemplateColumn` may be null, throwing at render time for any row.

**Approve Panel in Table Cell**
The approval form (time input + priority input + Confirm/Cancel) is rendered inline inside a `<QuickGrid>` `TemplateColumn` cell. This makes the row expand vertically and shifts the column layout. The panel is also not scoped visually — it is easy to mistake which row it belongs to when the grid has many entries.

---

## Fluent UI Blazor Redesign Proposal

### Standing Request Form (Member)

**Purpose:** Allow a member to submit a standing tee time request — preferred day, time, tolerance, season dates, and their three additional players.

**Recommended Components**

| Purpose | Component |
|---|---|
| Multi-step guided flow | `<FluentWizard>` (3 steps: Schedule, Players, Review) |
| Day of week | `<FluentSelect>` |
| Requested time | `<FluentTimePicker>` with `Min="06:00"` `Max="18:00"` `Step="00:15"` |
| Tolerance | `<FluentSelect>` limited to 0 / 15 / 30 options only (remove 60) |
| Date range | `<FluentDatePicker>` × 2 with `Min="@today"` on StartDate and `Min="@startDate"` on EndDate |
| Player search/selection | `<FluentAutocomplete>` × 3, each bound to the member list with a search filter, displaying `MembershipNumber — LastName, FirstName`; selected items shown as `<FluentBadge>` chips |
| Duplicate player warning | `<FluentMessageBar>` (Severity=Warning) shown inline below the player section if any two selections match |
| Summary before submit | `<FluentCard>` read-only review card on Wizard step 3 |
| Submit action | `<FluentButton>` Appearance=Accent |

**Layout Sketch**

```
+-------------------------------------------------------------+
| Request a Standing Tee Time                                 |
|                                                             |
| [Step 1: Schedule] > [Step 2: Players] > [Step 3: Review]  |
|                                                             |
| Step 1                                                      |
| +------------------+  +--------------+  +----------------+ |
| | Day of Week      |  | Time         |  | Tolerance      | |
| | [FluentSelect]   |  | [TimePicker] |  | [FluentSelect] | |
| +------------------+  +--------------+  +----------------+ |
| +-------------------------+  +----------------------------+ |
| | Start Date [DatePicker] |  | End Date   [DatePicker]   | |
| +-------------------------+  +----------------------------+ |
|                              [Next →]                       |
|                                                             |
| Step 2                                                      |
| Player 2: [FluentAutocomplete ________________] [×Badge]   |
| Player 3: [FluentAutocomplete ________________] [×Badge]   |
| Player 4: [FluentAutocomplete ________________] [×Badge]   |
| [MessageBar: Duplicate player selected] (conditional)      |
|                              [← Back]  [Next →]            |
|                                                             |
| Step 3 — Review                                             |
| +-----------------------------------------------------------+|
| | FluentCard: Saturday · 09:30 · ±30 min                   ||
| | Season: Apr 21 – Oct 21, 2026                            ||
| | Players: You, A. Smith, B. Jones, C. Brown               ||
| +-----------------------------------------------------------+|
|                              [← Back]  [Submit Request]    |
+-------------------------------------------------------------+
```

---

### My Standing Requests (Member)

**Purpose:** Show a member all their standing tee time requests, their current status, approved time, priority, and allow cancellation.

**Recommended Components**

| Purpose | Component |
|---|---|
| Request list | `<FluentDataGrid>` (replaces `<QuickGrid>`) with column sorting |
| Status display | `<FluentBadge>` colour-mapped to status (Pending=Neutral, Approved=Success, Allocated=Informational, Unallocated=Warning, Denied/Cancelled=Caution) |
| Allocation history per request | `<FluentAccordion>` / expandable row — expands to show a per-week table of dates and Allocated/Unallocated outcome |
| Cancel action | `<FluentButton>` Appearance=Outline triggers a `<FluentDialog>` confirmation (replaces the inline Yes/No in the cell) |
| Cancellation dialog | `<FluentDialog>` with title "Cancel Standing Request?" and Confirm/Cancel buttons |
| Tolerance out-of-spec flag | `<FluentIcon>` warning icon next to the tolerance badge if `ToleranceMinutes > 30` |
| ApprovedBy / ApprovedDate | Shown in the expanded accordion row once those fields are added to the entity |
| Empty state | `<FluentEmptyState>` with a call-to-action link to the request form |

**Layout Sketch**

```
+-------------------------------------------------------------+
| My Standing Tee Time Requests          [+ New Request]     |
|                                                             |
| [FluentDataGrid]                                           |
| Day/Time          Season      Status     Appr.Time  Actions |
| Sat 09:30 ±30m   Apr–Oct     [Approved] 09:15 #2   [Cancel]|
|   v Expand: Week-by-week allocation history                |
|     Apr 19 — Allocated (Hole 1, 09:15)                    |
|     Apr 26 — Unallocated (conflict)                       |
|     May  3 — Allocated (Hole 1, 09:15)                    |
|                                                             |
| [FluentDialog on Cancel click]                             |
| "Cancel your Saturday 09:30 standing request?"             |
| [Confirm Cancellation]  [Keep Request]                     |
+-------------------------------------------------------------+
```

---

### Standing Tee Time Management (Admin/Staff)

**Purpose:** Review all standing tee time requests, approve or deny them with an accurate time and priority number, and monitor allocation outcomes across the season.

**Recommended Components**

| Purpose | Component |
|---|---|
| Request list | `<FluentDataGrid>` with multi-column sort and URL-persisted filter state |
| Status filter | `<FluentTabs>` (All / Draft / Approved / Allocated / Unallocated / Denied / Cancelled) with count badge on each tab |
| Approve workflow | `<FluentDialog>` — opens on Approve button click, pre-populated with the member's `RequestedTime`; contains `<FluentTimePicker>` and `<FluentNumberField>` |
| Priority conflict warning | `<FluentMessageBar>` inside the dialog if the entered priority number is already in use by another active request |
| Deny workflow | Separate `<FluentDialog>` with an optional deny-reason `<FluentTextArea>` |
| Approved time/priority display | `<FluentBadge>` for priority, plain text for time; show ApprovedBy and ApprovedDate as a tooltip or secondary line once fields exist |
| Per-request detail | `<FluentAccordion>` expand to show full player list, week-by-week allocation, and audit trail |
| ApprovedBy / ApprovedDate | Captured from `AuthStateProvider` in the Approve dialog and persisted to the entity; shown as a sub-row in the grid |

**Layout Sketch**

```
+-------------------------------------------------------------+
| Standing Tee Time Requests                                  |
|                                                             |
| [All 12] [Draft 4] [Approved 5] [Allocated 2] [Denied 1]  |
|  FluentTabs ─────────────────────────────────────────────  |
|                                                             |
| [FluentDataGrid]                                           |
| Member       Day/Time      Season     Status    Actions    |
| Smith, A.    Sat 09:30±30  Apr–Oct    [Draft]   [Approve]  |
|                                                  [Deny]    |
|   v Expand: full foursome, week allocations, audit log     |
|                                                             |
| [Approve FluentDialog — opens on Approve click]            |
| +-----------------------------------------------------------+|
| | Approve Standing Request — Smith, A.                     ||
| | Approved Time: [FluentTimePicker pre-filled 09:30]       ||
| | Priority #:    [FluentNumberField min=1]                  ||
| | [MessageBar: Priority #2 already assigned to Jones, B.]  ||
| | [Confirm Approval]              [Cancel]                 ||
| +-----------------------------------------------------------+|
|                                                             |
| [Deny FluentDialog]                                        |
| +-----------------------------------------------------------+|
| | Deny Request — Smith, A.                                 ||
| | Reason (optional): [FluentTextArea]                      ||
| | [Confirm Denial]                [Cancel]                 ||
| +-----------------------------------------------------------+|
+-------------------------------------------------------------+
```

---

## Notes

1. **Tolerance cap (immediate fix required):** Remove the `±60 min` option from `StandingRequest.razor` line 89. This is a one-line change that enforces the spec without any backend work.

2. **Approve panel pre-population (immediate fix required):** In `StandingTeeTimes.razor`, `OpenApprovePanel` at line 220 must set `approvedTimeStr = context.RequestedTime.ToString("HH:mm")` instead of `"08:00"`. This requires passing the request object (or its time) into the method rather than just the id.

3. **Navigation property NPE (blocking):** Both `GetForMemberAsync` and `GetAllAsync` must include `BookingMember.User` and `AdditionalParticipants.User` via EF `.Include()` chains before the FluentDataGrid redesign is implemented. Without this fix any redesign will still crash at render time.

4. **ApprovedBy / ApprovedDate:** Adding these fields to the `StandingTeeTime` entity and capturing them in `ApproveAsync` is a prerequisite for surfacing them in the redesigned `<FluentDialog>` and the member accordion history view.

5. **Weekly allocation history:** The member accordion view and the admin expand panel both depend on a per-week allocation record being stored (e.g. a `StandingTeeTimeAllocation` child entity with `WeekDate`, `Status`, `AllocatedTime`, `CourseHole`). This data model change should be scoped before the UI work begins.

6. **URL-persisted filter state:** The admin `<FluentTabs>` filter should read/write a `?status=Draft` query parameter via `NavigationManager` so deep-linking and browser-back work correctly.

7. **`<FluentWizard>` availability:** Verify the version of the `Microsoft.FluentUI.AspNetCore.Components` package in use includes `<FluentWizard>`. It was introduced in v4.x; if the project is on an earlier version, a manual step-indicator using `<FluentStepper>` or a custom component is the fallback.
