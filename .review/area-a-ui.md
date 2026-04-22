# Area A: Membership Applications — UI Analysis & Fluent UI Redesign

## Current UI Issues

### 1. Form Validation — Apply.razor

**Missing fields**

- No signature field and no applicant declaration/consent checkbox. The gap analysis confirmed these are absent; submitting the form records no proof of consent.
- No city or province fields. The address section captures a single free-text `Address` line and a postal code but no separate city/province, which complicates deliverability and committee searches.
- The `DateOfBirth` field uses `DateTime.Today.AddYears(-30)` as a silent default, so an applicant who never touches the date will silently submit with an incorrect birthdate. There is no minimum-age check client-side (`[CustomValidation]` or `[Range]` on the derived age); the gap analysis notes age eligibility is not validated at all.

**Wrong or weak validators**

- `DateOfBirth` has `[Required]` but because `DateTime` is a value type the attribute can never fire for a non-nullable field; it will always pass. Age range validation is absent entirely.
- Sponsor IDs are validated only for existence in `MemberShips`, not for Shareholder status or tenure (gap analysis confirmed). No `[Range(min:1)]` guard prevents 0 or negative IDs from reaching the server.
- There is no `[MaxLength]` on any text field (`FirstName`, `LastName`, `Occupation`, etc.), so the model imposes no upper bound before hitting the database.

**Error feedback on submit failure**

- The return value of `SubmitMembershipApplicationAsync` is discarded (`await ApplicationService.SubmitMembershipApplicationAsync(application)`). If the service returns a failure indicator rather than throwing, the success branch always runs silently. The gap analysis listed this as a confirmed silent failure.
- Validation messages appear in a `<ValidationSummary>` block at the top and also inline via `<ValidationMessage>`, but the two duplicate each other and the summary scrolls out of view on longer forms. There is no focus-trap to guide the user to the first error.
- The submit button becomes `_submitted = true` on success, hiding the form, but on failure the form is still shown with no visual distinction indicating which card/section contains the problem.

### 2. UX Flow — Apply.razor

The entire application is a single long scrolling page with three Bootstrap cards stacked vertically (Personal Information, Employment Information, Membership Details). Issues:

- A new applicant has no sense of progress or how many steps remain. There is no step indicator or breadcrumb.
- The Membership Details card, which contains the legally significant choices (level, sponsors), is at the bottom with no emphasis that distinguishes it from the administrative sections above.
- Sponsors are entered as raw integer IDs with no lookup or auto-complete. An applicant is unlikely to know another member's internal database ID; this is a UX dead end.
- After a successful submit the `_submitted` flag hides the form and the `_successMessage` alert is shown, but there is no "What happens next?" guidance, no estimated timeline, and no confirmation email reference.
- The page has no `[Authorize]` attribute, so it is publicly accessible without authentication (gap analysis confirmed). Unauthenticated access also means the form cannot pre-populate known email/name from the logged-in user's identity.

### 3. ApplicationInbox — ApplicationInbox.razor

**Information density**

The table shows: First Name, Last Name, Email, Requested Level, Status, and one action link. Missing columns of material value to the committee:

- Submission date / age of application (how long has it been waiting?).
- Applicant's age (derived from date of birth) — relevant to eligibility.
- Sponsor names (committee members may need this at a glance to spot conflicts of interest).

**Filtering and sorting**

- Only status filtering is provided; there is no sort on any column, no name/email search, and no date-range filter.
- The filter requires a separate "Search" button click rather than reacting automatically to the dropdown change (`OnStatusFilterChanged` sets `_selectedStatus` but does not call `LoadApplicationsAsync`). The committee must remember to click Search after changing the dropdown.
- The default filter hides `Accepted` and `Denied` records but offers no way to see the full historical archive.
- No pagination is implemented; if there are many pending applications they all load at once.

**Status display**

- Statuses are displayed as Bootstrap `badge` elements styled by `UiHelpers.GetStatusBadgeClass`. Colour choices are not visible here but badge-only status on a plain table row gives no additional visual hierarchy.

### 4. ReviewApplication — ReviewApplication.razor

**Status transitions**

- The valid-transitions list is built as: every `ApplicationStatus` enum value except the current one, unless the application is `Accepted` or `Denied` (in which case no transitions are allowed). This means a `Submitted` application can be moved directly to `Accepted` or `Denied` without going through `OnHold` or `Waitlisted`, which may be intentional, but also means a `Denied` application can never be reopened — even by an administrator. There is no confirmation dialog before an irreversible decision (`Accepted`/`Denied`); a mis-click submits immediately.
- There is no "Notes / Reason" field on the decision panel. Deny with no recorded reason gives the committee no audit trail.
- When `Accepted` is selected a level dropdown appears, but it lists all levels regardless of the applicant's requested level; there is no pre-selection and no visual indication of what the applicant originally requested in the same section.

**Approval workflow clarity**

- The decision controls (status select + Submit Decision button) live in a narrow right-hand `col-lg-4` sidebar. On screens narrower than the lg breakpoint the sidebar stacks below all the applicant detail cards, requiring the reviewer to scroll past all the detail before acting.
- Sponsor names are resolved (`GetSponsorName`) and formatted as `"FirstName LastName (MemberNumber)"` — good — but there is no indication of sponsor eligibility (Shareholder status, tenure). A reviewer has no in-page way to verify sponsor credentials.
- There is no link back to a sponsor's member profile.
- The `_resultMessage` success/error banner appears at the top of the page but the page does not scroll to it, so if the reviewer has scrolled down it is easy to miss.

### 5. Accessibility and General Component Quality

- All three pages use Bootstrap-class HTML components (`<select>`, `<table>`, `<button>`) rather than semantic Fluent UI components, providing inconsistent keyboard navigation patterns.
- The Apply page uses `<InputSelect>` (Blazor) for membership level but raw `<select>` (HTML) for the status dropdown on the Inbox, mixing Blazor and plain HTML event patterns (`@bind` vs. `@onchange`).
- The ReviewApplication page uses a plain `<select>` with `@bind` for the decision dropdown, while Apply uses `<InputSelect>` — inconsistent within the same feature area.
- No `aria-label` or `aria-describedby` attributes are present on any interactive control beyond what Bootstrap provides by default.
- Spinner elements use `aria-hidden="true"` on the spinner icon and `role="status"` correctly on the container — this is done well.
- The Apply page's `InputDate` for date of birth provides no `min` or `max` attribute, so the browser's native date picker imposes no age constraint.

---

## Fluent UI Blazor Redesign Proposal

### Apply Page

- **Purpose:** Allow a prospective member (authenticated or unauthenticated guest, pending auth decision) to submit a complete, valid membership application in a guided, confidence-inspiring flow.

- **Components:**
  - `<FluentWizard>` — wraps the three logical sections (Personal Information, Employment Information, Membership & Sponsors) as discrete steps with a visible step indicator and Next/Back navigation.
  - `<FluentTextField>` — replaces all `<InputText>` fields. Supports `Label`, `Placeholder`, `Required`, `MaxLength`, `aria-describedby`, and inline `ErrorMessage` binding without a separate `<ValidationMessage>`.
  - `<FluentDatePicker>` — replaces `<InputDate>` for date of birth. Accepts `Min` and `Max` props to enforce an age floor (e.g., 18 years before today) and age ceiling directly in the control.
  - `<FluentSelect>` — replaces `<InputSelect>` for membership level. Supports a `Placeholder` option and data binding with a richer dropdown experience.
  - `<FluentNumberField>` or `<FluentAutocomplete>` — for sponsor IDs. Ideally a `<FluentAutocomplete>` backed by a server-side member search (name → ID lookup) so applicants do not need to know raw IDs.
  - `<FluentCheckbox>` — for applicant declaration / consent ("I confirm the information above is accurate and I have read the rules of the club.") and an electronic signature acknowledgement.
  - `<FluentButton Appearance="ButtonAppearance.Accent">` — Submit Application on the final wizard step.
  - `<FluentMessageBar>` (`Intent="MessageBarIntent.Success"` / `MessageBarIntent.Error"`) — replaces the Bootstrap alert divs for success/failure feedback, shown inline within the wizard footer rather than at the top of the page.
  - `<FluentProgressRing>` — replaces the Bootstrap spinner while submitting.

- **Layout:**
  ```
  [ Page header: "Apply for Membership" ]
  [ FluentWizard ]
    Step 1: Personal Information
      [ FluentTextField: First Name ]  [ FluentTextField: Last Name ]
      [ FluentDatePicker: Date of Birth (max = today - 18 years) ]
      [ FluentTextField: Phone ]  [ FluentTextField: Alternate Phone (optional) ]
      [ FluentTextField: Address ]
      [ FluentTextField: City ]  [ FluentTextField: Province ]  [ FluentTextField: Postal Code ]
      [ Next → ]

    Step 2: Employment
      [ FluentTextField: Occupation ]  [ FluentTextField: Company Name ]
      [ ← Back ]  [ Next → ]

    Step 3: Membership & Sponsors
      [ FluentSelect: Requested Membership Level (with level description tooltip) ]
      [ FluentAutocomplete: Sponsor 1 (search by name) ]
      [ FluentAutocomplete: Sponsor 2 (search by name) ]
      [ FluentCheckbox: Declaration / applicant signature acknowledgement ]
      [ ← Back ]  [ Submit Application (Accent) ]

  [ On success: FluentMessageBar (success) + "What happens next?" paragraph ]
  ```

- **New UX patterns:**
  - Each wizard step validates only its own fields before allowing Next, preventing the user from reaching the final step with unresolved errors in earlier sections.
  - Step 3 shows a read-only summary of the applicant's name and chosen level so they can verify before final submission.
  - If the user is authenticated, Step 1 auto-populates `Email`, `FirstName`, and `LastName` from the identity claims, with a note that they can be edited.
  - Sponsor lookup uses a `<FluentAutocomplete>` debounced search by name; on selection it stores the member ID but displays the member's name — removing the raw-ID entry problem.
  - On success, display the application reference number and estimated review timeline ("Applications are typically reviewed within 30 days.").

---

### Application Inbox (Committee)

- **Purpose:** Allow committee members and admins to monitor all pending membership applications, triage by status or age, and navigate to individual applications for review.

- **Components:**
  - `<FluentDataGrid>` with `Items`, `TGridItem`, column sort (`SortBy`), and virtual scrolling or pagination — replaces the static Bootstrap table. Provides built-in keyboard navigation, column resizing, and accessible row selection.
  - `<FluentSelect>` — status filter dropdown, wired to trigger `LoadApplicationsAsync` on `@bind-Value` change (removing the manual Search button).
  - `<FluentSearch>` — free-text search field filtering by applicant name or email client-side (or server-side via debounced query).
  - `<FluentBadge Color="Color.Warning">`, `<FluentBadge Color="Color.Error">`, etc. — status indicators within the grid, using semantic Fluent colours rather than manually mapped Bootstrap classes.
  - `<FluentButton Appearance="ButtonAppearance.Outline">` — inline Review action per row, or use row-click navigation.
  - `<FluentPaginator>` — paginates the data grid if the full dataset is large.
  - `<FluentProgressRing>` — loading state while fetching applications.
  - `<FluentToolbar>` — wraps the filter controls in a consistent toolbar band above the grid.

- **Layout:**
  ```
  [ Page header: "Membership Application Inbox" ]
  [ FluentToolbar ]
    [ FluentSelect: Status filter (auto-applies on change) ]
    [ FluentSearch: Name / email search ]
    [ FluentBadge: count of displayed results ]

  [ FluentDataGrid ]
    Columns: Applicant Name (sortable) | Email | Requested Level | Submission Date (sortable) |
             Age | Status (FluentBadge) | Days Waiting (sortable) | Actions
    [ Review button → /membership/applications/{id} ]

  [ FluentPaginator ]
  ```

- **New UX patterns:**
  - The default sort is "Days Waiting" descending so the oldest unactioned applications surface immediately.
  - Submission Date and computed "Days Waiting" columns make queue age visible without opening each application.
  - Status filter change is reactive (no Search button needed); the toolbar also adds a "Show closed" toggle to include `Accepted` and `Denied` applications for historical reference.
  - Row-level `<FluentBadge>` for status uses semantic colour: `Submitted` = neutral, `OnHold` = warning, `Waitlisted` = informational, so the committee can scan the queue at a glance.

---

### Review Application (Committee)

- **Purpose:** Allow a committee member or admin to examine all details of a single membership application and record a binding decision (approve, deny, hold, waitlist), with an optional reason note and full audit trail.

- **Components:**
  - `<FluentCard>` — wraps applicant detail sections (Personal, Employment, Membership, Sponsors), replacing Bootstrap cards.
  - `<FluentBadge>` — current application status, displayed prominently in the page header area next to the applicant name.
  - `<FluentAccordion>` / `<FluentAccordionItem>` — collapses detail sections so the reviewer can focus on the section most relevant to their decision without scrolling.
  - `<FluentSelect>` — decision dropdown, bound two-way, filtered to only show valid next-state transitions. If the current state is terminal (`Accepted`/`Denied`), the dropdown is hidden and replaced with a read-only status display.
  - `<FluentSelect>` (conditional) — membership level selector, shown only when `Accepted` is chosen, pre-selecting the applicant's requested level.
  - `<FluentTextArea>` — "Notes / Reason" field, required when selecting `Denied`, optional otherwise. Text is persisted with the decision for audit purposes.
  - `<FluentDialog>` — confirmation dialog for irreversible decisions (`Accepted`, `Denied`): "Are you sure you want to approve/deny this application? This action cannot be undone." with Confirm / Cancel buttons.
  - `<FluentButton Appearance="ButtonAppearance.Accent">` — Submit Decision (disabled until a valid status and, where required, a level and/or reason are chosen).
  - `<FluentMessageBar>` — inline success/error feedback, rendered at the top of the decision panel (not the top of the page) so it is always visible after action.
  - `<FluentPersona>` or structured `<FluentCard>` — sponsor summary showing name, membership number, level, and a computed tenure/eligibility note, making sponsor credential verification possible without leaving the page.
  - `<FluentBreadcrumb>` — "Inbox → Review Application #123" navigation aid.

- **Layout:**
  ```
  [ FluentBreadcrumb: Inbox > Review #123 ]
  [ Page header: "Review Application — FirstName LastName"  FluentBadge: Status ]

  [ Two-column layout on lg+ screens, single column on smaller ]

  Left column (approx. 65%)
    [ FluentCard: Personal Information (accordion or always open) ]
      Name | DOB | Email | Phone | Address
    [ FluentCard: Employment ]
      Occupation | Company
    [ FluentCard: Membership Request ]
      Requested Level | Application date
    [ FluentCard: Sponsors ]
      [ FluentPersona: Sponsor 1 — Name, Member #, Level, Tenure ]
      [ FluentPersona: Sponsor 2 — Name, Member #, Level, Tenure ]

  Right column (approx. 35%)
    [ FluentCard: Make Decision (sticky on lg+) ]
      [ FluentBadge: Current Status ]
      [ FluentSelect: New Status (filtered transitions) ]
      [ FluentSelect: Membership Level (conditional, shown only for Accepted) ]
      [ FluentTextArea: Notes / Reason (required for Denied) ]
      [ FluentMessageBar: success or error after action ]
      [ FluentButton Accent: Submit Decision ]
      → triggers FluentDialog for Accepted / Denied

  [ FluentDialog: Confirmation ]
    "Confirm [Approve / Deny] Application?"
    Summary of decision and level (if applicable)
    [ Cancel ]  [ Confirm (Accent) ]
  ```

- **New UX patterns:**
  - The decision panel is `position: sticky` on large screens so the committee member can read the detail cards without the action controls scrolling off screen.
  - Sponsor `<FluentPersona>` cards compute and display eligibility status (e.g., "Shareholder — 5 years tenure") so the reviewer does not need to look up sponsors separately.
  - A `<FluentDialog>` confirmation gate prevents accidental terminal decisions; the dialog repeats the applicant name, chosen status, and chosen level as a final review summary.
  - The Notes/Reason field is surfaced for all decisions (not just Deny), enabling a complete audit trail. The field is required before the Submit button enables when the chosen status is `Denied`.
  - After a successful decision the page reloads the application (as it does today) but the decision panel collapses or is replaced by a read-only "Decision recorded" summary if no further transitions are available, rather than showing an empty dropdown.

---

## Notes

- The most urgent functional change required before any redesign is adding `[Authorize]` to `Apply.razor`; the page is currently publicly accessible with no authentication gate.
- The discarded return value of `SubmitMembershipApplicationAsync` in `Apply.razor` (line 258) must be replaced with a check-and-branch so submit failures surface to the user rather than silently showing the success message.
- The sponsor validation in `HandleSubmitAsync` (Apply.razor, lines 218–236) queries `MemberShips` by ID only. It must be extended to verify that each sponsor holds Shareholder status and meets the required tenure threshold.
- The `_validTransitions` calculation in `ReviewApplication.razor` (line 215–217) allows any status → any status transition (except from terminal states). This should be replaced with an explicit allowed-transition map (e.g., `Submitted` → `{OnHold, Waitlisted, Accepted, Denied}`; `OnHold` → `{Submitted, Waitlisted, Accepted, Denied}`; etc.) to prevent nonsensical transitions such as `Waitlisted` → `Submitted`.
- A `<FluentAutocomplete>` sponsor lookup requires a new lightweight API endpoint or Blazor service method returning member name/number matches; this should be scoped and rate-limited.
- Fluent UI Blazor v4 (`Microsoft.FluentUI.AspNetCore.Components`) is the current stable release and is compatible with Blazor Server Interactive render mode, matching the existing `@rendermode InteractiveServer` used throughout these pages.
