# Area B: Regular Tee Times — Prioritized Task List

## Critical (correctness bugs)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| C1 | Fix `MembershipLevelAvailabilityRule` upper-bound operator: change `a.EndTime >=` to `a.EndTime >` (strict less-than) to make the window half-open `[Start, End)` | H | XS | One-character fix in `ClubBaist.Domain2/Booking/Rules/MembershipLevelAvailabilityRule.cs` line 18; currently allows slots at exactly 15:00 for Silver/Bronze, contradicting the spec's "Before 3:00 PM" |
| C2 | Fix `TeeTimeAvailabilityPanel.CanBook` to match the corrected rule: both must use the same boundary convention (currently UI uses `< EndTime` strict, rule uses `>= EndTime` inclusive — they disagree, causing split display/enforcement behaviour) | H | XS | `ClubBaist.Web/Components/Pages/TeeTimes/TeeTimeAvailabilityPanel.razor` line 507; do this in the same commit as C1 so they move together |
| C3 | Implement `AdvanceBookingWindowRule` (7-day cap) and register it in the booking pipeline | H | S | Rule skeleton is already documented in the design report; mirrors `PastSlotRule`; register in `ClubBaist.Services2/ServiceCollectionExtensions2.cs`; also enforce `MaxDate` (today + 7) in the availability panel date picker |
| C4 | Fix member search in `StaffConsole.razor`: add `MembershipNumber` to the `Where` clause in `SearchMembers` — the label already says "name or member number" but the query only filters on `FirstName`/`LastName` | H | XS | Pure SQL/LINQ fix; no model changes needed |
| C5 | Remove `@if (true)` dead code in `StaffConsole.razor` line 114 and replace with a real condition (or remove the wrapper) | H | XS | Latent logic error; "Book on Behalf" button is always shown regardless of member status |

## High Priority (spec gaps)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| H1 | Seed `Copper` (Social) `MembershipLevel` and add a guard that blocks Copper members from booking (no golf privileges per spec) | H | S | `AppDbContextSeed.cs`; the `MembershipLevelAvailabilityRule` will block them automatically once Copper exists with no availability rows — seeding is the only required change, but add a test to confirm |
| H2 | Add `NumberOfCarts` (`int`, default 0) and `EmployeeName` (`string?`) to `TeeTimeBooking` entity, generate migration, expose `NumberOfCarts` in `CreateReservation.razor` form, and display both in `StaffConsole.razor` tee-sheet grid | H | M | Two missing fields required by the spec's daily tee sheet; `EmployeeName` is nullable (absent for self-service bookings) |
| H3 | Add "Day of Week" column to `StaffConsole.razor` Reservations grid and "Phone" column sourced from `ClubBaistUser.PhoneNumber` via the `BookingRow` projection | H | S | `BookingRow` record must include phone; query must join to `ClubBaistUser`; no model migration needed |
| H4 | Add Special Event management UI to Staff Console: a tab/panel where staff can create, edit, and delete `SpecialEvent` records | H | L | `SpecialEvent` is already modelled and enforced by `SpecialEventBlockingRule`; this is purely a missing UI surface; scope includes list, create dialog, and delete confirmation |
| H5 | Replace `TeeTimeSlot.Start` `DateTime` PK with a surrogate `int Id`; update `TeeTimeBooking.TeeTimeSlotStart` FK to `TeeTimeBooking.TeeTimeSlotId` (`int`); remove the now-redundant `[Index(nameof(Start))]` attribute | H | L | Precision collision and FK brittleness risks; requires EF migration + update of all query/join sites in services and rules; `Start` stays as a unique-constrained column |
| H6 | Add integration tests for Silver/Bronze membership-level time windows: confirm allow at 14:59, reject at 15:00 (post-fix), reject at 15:01; confirm allow at 17:30 for Silver, 18:00 for Bronze; seed real availability rows in test infrastructure | H | M | Currently no test exercises the seeded `MembershipLevelTeeTimeAvailability` rows end-to-end; boundary defect (C1/C2) must be fixed first |

## Medium Priority (design improvements)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| M1 | Surface booking rejection reasons to the user: change `BookingService.CreateBookingAsync` (and `CancelBookingAsync`) to return a discriminated result or typed exception instead of bare `bool`/`false`; update `CreateReservation.razor` and `StaffConsole.razor` to display the specific reason | M | M | Currently all rejections show a generic "Unable to create booking" message; `RejectionReason` is already tracked in `TeeTimeEvaluation` |
| M2 | Add date-range validation to `StaffConsole.razor`: enforce `dateTo >= dateFrom` client-side and re-validate server-side before executing the EF query; add a maximum window (e.g., 90 days) to prevent runaway queries | M | S | Currently an invalid range silently returns 0 results with no feedback |
| M3 | Add public holiday modelling: either extend `SpecialEvent` with an `IsHoliday` flag or introduce a dedicated `Holiday` entity/table; update `MembershipLevelAvailabilityRule` to treat holiday-weekdays as weekend days for Silver/Bronze access window selection | M | L | Without this, Silver/Bronze members get weekday rules on holidays (e.g., Victoria Day, Canada Day), contradicting the spec's "Weekday/Weekend or Holiday" distinction |
| M4 | Add no-show / reprimand tracking: add a `NoShowRecord` or `Strike` entity linked to `TeeTimeBooking` and `ClubBaistUser`; expose a "Mark No-Show" action in the Staff Console tee sheet | M | L | Spec says members who fail to cancel will be "reprimanded appropriately"; currently no record of no-shows exists |
| M5 | Make Staff Console tabs bookmarkable: update the URL hash on tab switch so browser back/forward and deep-linking work correctly | M | S | Currently tab state is in-memory only; Blazor router or query-string approach both viable |
| M6 | Add a "Today" quick-select button to the Staff Console date picker and add a date-scoped, time-sorted daily tee-sheet mode (not just a filtered search grid) | M | M | Staff currently have to manually set the date range to see one day's sheet |
| M7 | Add write-up tests for `AdvanceBookingWindowRule` (C3) covering: allow at day 0, allow at day 7, reject at day 8; also add an integration test confirming the rule is registered in the pipeline | M | S | Depends on C3 being implemented first |
| M8 | Clarify and document Shareholder/Associate → Gold equivalence in seed data: add a comment or a named constant that explicitly maps `"SH"` and `"AS"` to Gold-tier access; confirm whether a separate `"GO"` Gold level is needed | M | XS | Currently the mapping is implicit via a `default` case; silent behaviour change risk if new levels are added |

## Low Priority (polish)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| L1 | Replace Bootstrap spinner in `Availability.razor` with a skeleton/partial content render during auth/member lookup | L | XS | Pure UX polish; no logic changes |
| L2 | Replace inline Confirm/No cancel pattern in `StaffConsole.razor` with a `<FluentDialog>` (or Bootstrap modal) confirmation; prevents confirm state appearing on wrong row after scroll/re-render | L | S | Also apply to `MyReservations.razor` cancel flow |
| L3 | Migrate `StaffConsole.razor` and `TeeTimeAvailabilityPanel` from Bootstrap `QuickGrid` to `<FluentDataGrid>` with resizable/sortable columns; add export/print action for the daily tee sheet | L | XL | Full Fluent UI redesign; columns become resizable and a column chooser becomes available; block on H2/H3 (new columns must exist first) |
| L4 | Migrate `CreateReservation.razor` booking flow to a `<FluentDialog>` modal triggered from the availability grid, with `<FluentAutocomplete>` for participant search and `<FluentPersona>` chips per participant | L | XL | Fluent UI redesign; depends on M1 (rejection reasons) to populate the `<FluentMessageBar>` inside the dialog |
| L5 | Migrate `MyReservations.razor` to `<FluentTabs>` (Upcoming / Past) with `<FluentDataGrid>` and virtualization | L | L | Low urgency; current list is functional |
| L6 | Show player names in `MyReservations.razor` grid (currently shows count only); the spec says members want to see names of all players scheduled to play | L | S | `ReservationDetail.razor` already shows full names; surface them in the list view |
| L7 | Fix `ReservationDetail.razor` past-booking view: the Players card is hidden behind `>= DateTime.Now`; past bookings should still show the player list (read-only, no edit/cancel buttons) | L | XS | `ClubBaist.Web/Components/Pages/TeeTimes/ReservationDetail.razor` lines 133–154 |
| L8 | Add `SeasonService2.GenerateSlots` unit test asserting the alternating 7/8-minute pattern and that the spec's sample start times (7:00, 7:07, 7:15, 7:22…) are produced correctly | L | XS | Low risk since the pattern matches sample data, but no regression guard exists |
| L9 | Inject `TimeProvider` (or `IClock`) into `PastSlotRule` and `AdvanceBookingWindowRule` instead of calling `DateTime.Now` directly; enables deterministic unit tests and future timezone support | L | S | Currently both rules are tied to server wall-clock; low urgency until multi-timezone support is needed |

---

## Grouped Tasks (must go together)

### Group 1 — Boundary Bug Fix (C1 + C2)
Fix the `MembershipLevelAvailabilityRule` upper-bound operator (C1) and align `TeeTimeAvailabilityPanel.CanBook` to the same convention (C2) in a single commit. Fixing only one side leaves the display/enforcement split behaviour intact.

### Group 2 — Advance Booking Window (C3 + M7)
Implement `AdvanceBookingWindowRule` (C3) together with its unit and integration tests (M7). The rule has no value in production without test coverage that pins its boundary semantics.

### Group 3 — Tee Sheet Missing Fields (H2 + H3)
Add `NumberOfCarts` and `EmployeeName` to `TeeTimeBooking` (H2) in the same migration that adds "Phone" and "Day of Week" to the `StaffConsole.razor` grid (H3). Both require touching the `BookingRow` projection and the grid column layout; splitting them creates two migrations touching the same files.

### Group 4 — PK Surrogate Key Refactor (H5)
`TeeTimeSlot` PK change (H5) must be done atomically with all FK sites. All rules, services, queries, and the `TeeTimeBooking` entity that reference `TeeTimeSlotStart` must be updated in the same PR. No partial migration is safe.

### Group 5 — Rejection Reason Pipeline (M1 + L2 + L4)
Surfacing rejection reasons (M1) is a prerequisite for meaningful error display in the cancel confirmation dialog (L2) and the booking dialog (L4). Do M1 first; L2 and L4 can follow independently once the service contract returns a typed result.

### Group 6 — Holiday Support (M3)
Holiday modelling (M3) requires coordinating changes to the data model, `MembershipLevelAvailabilityRule`, and seed data. It should also include tests for the holiday-weekday → weekend-rule path. Keep together to avoid a half-landed feature where holidays are modelled but the rule ignores them.

### Group 7 — No-Show / Reprimand (M4)
No-show tracking (M4) requires a new entity, a migration, a service method, and a Staff Console UI action ("Mark No-Show"). These are tightly coupled and must ship together; a schema with no UI or a UI with no schema is not useful.

---

## Independent Tasks (can be parallelized)

The following tasks have no dependencies on each other and can be assigned to different developers simultaneously:

| Task | Depends On |
|---|---|
| C4 — Fix member search query to include `MembershipNumber` | Nothing |
| C5 — Remove `@if (true)` dead code | Nothing |
| H1 — Seed Copper membership level | Nothing |
| H4 — Special Event management UI | Nothing (model already exists) |
| H6 — Integration tests for Silver/Bronze time windows | C1 + C2 must be fixed first |
| M2 — Date-range validation in Staff Console | Nothing |
| M5 — Bookmarkable Staff Console tabs | Nothing |
| M6 — "Today" button and daily tee-sheet mode | Nothing |
| M8 — Document Shareholder/Associate → Gold mapping | Nothing |
| L1 — Replace Bootstrap spinner with skeleton | Nothing |
| L6 — Show player names in MyReservations grid | Nothing |
| L7 — Fix ReservationDetail past-booking player list | Nothing |
| L8 — GenerateSlots unit test | Nothing |
| L9 — Inject TimeProvider into time-dependent rules | C3 recommended first |
