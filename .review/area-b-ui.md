# Area B: Regular Tee Times — UI Analysis & Fluent UI Redesign

## Current UI Issues

### Availability.razor
- The page is a thin shell that delegates entirely to `<TeeTimeAvailabilityPanel>` (not reviewed here). There is no visible date picker, slot grid, or restriction messaging on the page itself — all of that lives in the child component.
- During the auth/member lookup, the entire page shows only a Bootstrap spinner; no skeleton or partial content is rendered.
- No date-range cap is enforced on whatever date picker the panel exposes (known issue confirmed: the page passes no date-boundary props to the panel).
- Slot restriction reasons are not surfaced anywhere in this file; if the panel silently blocks a booking, members see no explanation (known issue confirmed).

### StaffConsole.razor
- **Missing tee-sheet columns**: The Reservations grid shows only Date / Time / Booked By / Member # / Players / Status. Phone number, cart count, employee name, and day-of-week are absent (known issue confirmed). The Members sub-grid (per-member reservations) has the same gap.
- **No daily tee-sheet view**: There is no date-scoped, time-sorted "day sheet" mode. Staff must enable a custom date range and filter manually to approximate one. There is no "today" quick-select button.
- **Bootstrap `QuickGrid`, not a proper data grid**: Both grids use `<QuickGrid>` with raw Bootstrap table classes. Columns are not resizable, there is no column chooser, and print/export is absent.
- **Cancel UX is inline and fragile**: Confirm/No buttons replace the Cancel button in the same cell. If the user scrolls or the grid re-renders during debounce, the confirm state (`cancelConfirmId`) may appear on the wrong row visually. No modal confirmation dialog is used.
- **Generic error messages**: `ConfirmCancel` catches all exceptions and shows "Failed to cancel booking. Please try again." The `BookingService.CancelBookingAsync` path that returns `false` shows "Unable to cancel booking." — neither message tells staff why (known issue confirmed; same pattern applies to booking rejection in `CreateReservation`).
- **Member search does not search by membership number**: The `SearchMembers` query filters only on `FirstName` and `LastName` using `EF.Functions.Like`. The label says "Filter by name or member number" but `MembershipNumber` is not included in the `Where` clause.
- **No special-event admin UI**: No tab, panel, or action exists for creating or managing special events (known issue confirmed).
- **Date range has no upper cap**: The "To" date input accepts any future date with no validation that `dateTo >= dateFrom` and no maximum window (e.g., 90 days). An invalid range silently returns 0 results.
- **Tab state is not bookmarkable**: Switching between Members and Reservations tabs does not update the URL, so deep-linking to either view is impossible and browser back/forward behaves unexpectedly.
- **`@if (true)` dead code**: Line 114 has `@if (true)` wrapping the "Book on Behalf" button — the condition was never replaced with a real active-member check. A comment acknowledges the removal but the wrapper remains.

---

## Fluent UI Blazor Redesign Proposal

### Availability / Browse Tee Times
- **Purpose**: Member browses available slots filtered to their membership level.
- **Components**:
  - `<FluentCalendar>` for date selection with a configurable `MinDate` (today) and `MaxDate` (e.g., 14 days out, enforced server-side cap).
  - `<FluentDataGrid>` for time slots with columns: Time / Spots Remaining / Booked Members / Book.
  - `<FluentBadge>` for availability status (Open / Limited / Full / Restricted).
  - `<FluentTooltip>` on restricted badges to display the restriction reason (e.g., "Members-only until 10 AM on weekends").
  - `<FluentProgressRing>` replaces the Bootstrap spinner during initial load.
- **Layout**: Two-column — `<FluentCard>` with the calendar on the left (~25 % width); `<FluentDataGrid>` on the right (~75 %) updates reactively when a date is selected. No page reload.
- **Fix**: Enforce date-range cap via a `MaxDate` prop passed down to the calendar and validated server-side. Surface restriction reasons via `<FluentTooltip>` on the badge rather than silently hiding slots.

### Book a Tee Time
- **Purpose**: Member selects participants and confirms a booking.
- **Components**:
  - `<FluentDialog>` (modal) triggered by a "Book" button in the availability grid row.
  - `<FluentAutocomplete>` for searching and adding additional players (members or guests).
  - `<FluentPersona>` chips for each selected participant with a remove button.
  - `<FluentButton Appearance="Accent">` for Confirm, `<FluentButton>` for Cancel.
  - `<FluentMessageBar>` inside the dialog to show validation errors or the specific rejection reason (replaces the generic error — known issue fix).
- **Layout**: Dialog header shows slot date/time; body has the participant selector and persona list; footer has action buttons.

### My Reservations
- **Purpose**: Member views upcoming and past bookings and can cancel.
- **Components**:
  - `<FluentDataGrid>` with virtualization for large history sets.
  - `<FluentBadge>` for status (Upcoming = Accent color, Past = Neutral).
  - `<FluentDialog>` for cancel confirmation instead of the current inline Confirm/No pattern.
  - `<FluentTab>` / `<FluentTabs>` to separate Upcoming and Past without mixing them in one list.
- **Layout**: Tabs at top (Upcoming / Past); grid below; cancel opens a small confirmation dialog with the specific booking details echoed back to prevent accidental cancellation.

### Staff Console (Daily Tee Sheet)
- **Purpose**: Pro shop staff views and manages the day's tee sheet.
- **Components**:
  - `<FluentTabs>` replacing the Bootstrap `nav-tabs` for Members / Tee Sheet / Reservations views. URL hash updated on tab switch for bookmarkability.
  - `<FluentDatePicker>` at the top of the Tee Sheet tab with a "Today" quick-select button; drives the day view.
  - `<FluentDataGrid>` with sortable, resizable columns:
    - Tee Sheet view columns: Time / Day of Week / Member / Member # / Players / Phone / Carts / Employee / Status / Actions.
    - Reservations search view: same columns plus Booked By.
  - `<FluentButton IconStart="@(new Icons.Regular.Size16.Add())">` for adding a walk-in booking.
  - `<FluentDialog>` for cancel confirmation (replaces inline confirm buttons).
  - `<FluentMessageBar>` for success/error feedback inline below the toolbar rather than dismissible Bootstrap alerts at the top of the page.
  - `<FluentSearch>` replacing the plain `<input type="text">` for member and reservation search; fix the member search query to also filter on `MembershipNumber`.
  - Export/Print: `<FluentButton>` invoking a server-side PDF/CSV render of the day's sheet.
- **Layout**: Full-width. Top bar: tab selector + date picker + Today button + Add Walk-in button + Export button. Grid below fills remaining height with sticky header.

## Notes

- The `@if (true)` block in `StaffConsole.razor` (line 114) should be removed; the "Book on Behalf" button should be shown unconditionally or gated on a real condition.
- The member search query (`SearchMembers`) must be extended to include `MembershipNumber` in its `Where` clause to match the label's stated behavior.
- `BookingService.CancelBookingAsync` returning `false` vs. throwing an exception is an ambiguous contract; the service should return a discriminated result or throw a typed exception so the UI can display a specific reason.
- Date range validation (`dateTo >= dateFrom`) must be enforced client-side in `<FluentDatePicker>` and re-validated server-side before the EF query executes.
- Special-event admin UI (create/edit/delete special events, override slot availability) should be added as a fourth tab in the Staff Console, with `<FluentDataGrid>` listing events and `<FluentDialog>` for create/edit.
