# Area C: Standing Tee Times — Prioritized Task List

## Scoring Key
- **Impact:** H = high, M = medium, L = low
- **Effort:** XS = 1–2h, S = half-day, M = 1–2 days, L = 3–5 days, XL = week+
- **Priority Tiers:** Critical (bugs blocking normal use) → High (spec gaps) → Medium (design improvements) → Low (future features / full Fluent UI rewrite)

---

## Critical

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| C1 | Fix missing `.Include()` in `GetAllAsync` and `GetForMemberAsync` — both methods use `AsNoTracking()` with no navigation includes; accessing `context.BookingMember.User.FirstName` and `p.User.FirstName` on the rendered pages throws `NullReferenceException` at runtime | H | XS | Add `.Include(s => s.BookingMember).ThenInclude(m => m.User).Include(s => s.AdditionalParticipants).ThenInclude(p => p.User)` to both queries. Without this fix, the admin grid and member history page crash on every page load. |
| C2 | Fix `StandingTeeTimes.razor` approve panel defaulting to `"08:00"` instead of the member's requested time — `OpenApprovePanel` hard-codes `approvedTimeStr = "08:00"` regardless of the selected request | H | XS | One-line fix: `approvedTimeStr = context.RequestedTime.ToString("HH:mm")`. Pass the request object (or its `RequestedTime`) into the method. This is a direct cause of incorrect approved times being committed to the database. |
| C3 | Remove `±60 min` tolerance option from `StandingRequest.razor` — the business spec caps tolerance at ±30 min; the 60-minute option lets members submit data the spec forbids | H | XS | Remove the `<option value="60">±60 min</option>` line. Also tighten `[Range(0, 120)]` to `[Range(0, 30)]` on `StandingTeeTime.ToleranceMinutes`. |

---

## High

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| H1 | Add Shareholder membership-level check to `SubmitRequestAsync` — the service currently trusts that the `BookStandingTeeTime` claim is only held by Shareholders, but a misconfigured claim grant lets any member submit successfully | H | S | Load the booking member's `MembershipLevel.ShortCode` (or `MemberType` enum once Area A M1 lands) and reject if not Shareholder. This is the trust-boundary enforcement that the Razor page does not enforce. |
| H2 | Add `ApprovedByUserId` (string) and `ApprovedDateUtc` (DateTime?) to `StandingTeeTime` and update `ApproveAsync` to accept and persist the acting user's identity | M | S | Migration + service signature change: `ApproveAsync(int id, TimeOnly approvedTime, int? priorityNumber, string approvedByUserId)`. Surfaces as audit trail in admin grid and member history view. |
| H3 | Add `PriorityNumber` uniqueness enforcement — two requests can currently receive the same priority, making allocation order undefined | M | S | Options: (a) filtered unique DB index `WHERE PriorityNumber IS NOT NULL`; (b) service-level conflict check in `ApproveAsync` before assigning. Option (b) is simpler and shows a useful error message; option (a) provides the DB safety net. Do both. |
| H4 | Fix `GeneratedBookings` `[NotMapped]` property — the property is not mapped to any navigation, so EF cannot populate it and it is structurally dead; it will always be an empty list | M | XS | Remove the `[NotMapped] GeneratedBookings` property. Once the `StandingTeeTimeWeekAllocation` child table exists (M1 below), replace it with the properly mapped `WeekAllocations` navigation. |
| H5 | Add `StartDate >= today` validation (client and service) — members can currently submit requests for dates already in the past | M | XS | Add `[CustomValidation]` or inline `if (startDate < DateTime.Today)` before `SubmitRequestAsync`. Mirror the guard in the service. |
| H6 | Add priority conflict warning to the admin approve UI — currently the priority number input accepts any value with no duplicate detection | M | S | Before calling `ApproveAsync`, query for any existing active request with the same priority number and show a `<FluentMessageBar>` (or Bootstrap alert) inside the approve panel. Pairs with H3. |
| H7 | Refine active-request uniqueness constraint from "one request per member total" to "one request per member per `(RequestedDayOfWeek, overlapping date range)`" | M | S | The current "one active request" blunt check blocks a member from submitting a new season request while their current-season request is still active. Correct semantics: reject only if date ranges overlap for the same day of week. |

---

## Medium

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| M1 | Introduce `StandingTeeTimeWeekAllocation` child entity — one row per calendar week per request; fields: `WeekStartDate` (`DateOnly`), `Status` (`WeekAllocationStatus` enum), nullable `TeeTimeBookingId` FK, `UnallocatedReason?` | H | L | This is the prerequisite for (a) meaningful per-week status display in the member history view, (b) the allocation engine (L1), and (c) moving `Allocated`/`Unallocated` off the parent entity. One migration; do not remove parent enum values until L1 is implemented. |
| M2 | Add `CreatedAtUtc` timestamp to `StandingTeeTime` — needed as a secondary sort key when two requests share the same `PriorityNumber` | L | XS | Set in `SubmitRequestAsync` before `SaveChangesAsync`. One nullable column + migration. |
| M3 | Add validation: requested day of week must fall within the `StartDate`–`EndDate` range at least once | M | XS | Simple LINQ check: `Enumerable.Range(0, (endDate - startDate).Days + 1).Any(i => startDate.AddDays(i).DayOfWeek == requestedDayOfWeek)`. |
| M4 | Add `EndDate <= StartDate` guard to `SubmitRequestAsync` (and client-side date picker `min="@startDate"` on EndDate) | M | XS | Gap analysis confirms this is missing. Pairs well with H5 in a single "date validation" PR. |
| M5 | Add integration tests for: Shareholder enforcement (H1), tolerance cap (C3), priority number conflict (H3), start-date-in-past (H5), day-of-week not in range (M3) | M | M | The test suite covers the CRUD happy paths well but none of the new guards. Add one test per rule, use the existing `Domain2TestHost` pattern. |
| M6 | Add `DenyReason` (string?) to `StandingTeeTime` entity and capture it in `DenyAsync` — currently no reason is recorded; the member receives no explanation | L | S | Migration + service signature: `DenyAsync(int id, string? reason)`. Display in admin grid and member view. |
| M7 | Fix admin page authorization from `Admin`-only to also allow `Clerk` and `ProShopStaff` roles (depends on Area E C1 — new roles being created) | M | XS | One attribute change on `/admin/standing-teetimes`. Block on Area E C1. |
| M8 | Fix admin grid default sort — add "sort by PriorityNumber ascending" as the default view to align with the business allocation workflow | L | XS | Change the `QuickGrid` column to `InitialSortDirection="SortDirection.Ascending"` on priority. |
| M9 | Implement cancellation of an `Allocated` request: `CancelAsync` must also cancel or flag the already-generated `TeeTimeBooking` for that week | M | M | Currently `CancelAsync` sets status = Cancelled but leaves generated bookings intact. Block on M1 existing. |

---

## Low

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| L1 | Implement `StandingAllocationService` — the weekly allocation engine: load approved requests ordered by priority, find available tee slot within tolerance on the correct day of week, skip special-event-blocked slots, create `TeeTimeBooking`, set week-row status | H | XL | This is the most complex missing feature in the codebase. Block on M1 (`StandingTeeTimeWeekAllocation` entity). Separate service class from `StandingTeeTimeService` to keep concerns cleanly separated. |
| L2 | Add weekly recurrence trigger — a background `IHostedService` (or admin-triggered "Run Allocation" button) that calls `StandingAllocationService` once per week, generating `Pending` week rows for all active approved requests | H | L | Block on L1. The hosted service approach is simpler for the current scope; Hangfire/Quartz is appropriate if a richer scheduling UI is needed later. |
| L3 | Special-event conflict suppression during allocation — before placing a standing request on a given week, check if a `SpecialEvent` blocks the target slot and mark as `WeekAllocationStatus.Skipped` | M | M | Block on L1 and L2. Requires `SpecialEvent` entity integration (Area B concern). |
| L4 | Full Fluent UI redesign — `StandingRequest.razor` as `<FluentWizard>` (Step 1: Schedule, Step 2: Players, Step 3: Review) with `<FluentTimePicker>`, `<FluentDatePicker>`, `<FluentAutocomplete>` for player selection, duplicate-player `<FluentMessageBar>` | M | L | Design reference in area-c-ui.md. Requires Fluent UI package install (Area F). |
| L5 | Full Fluent UI redesign — `MyStandingRequests.razor` with `<FluentDataGrid>`, expandable per-week allocation history accordion, `<FluentDialog>` cancel confirmation, `<FluentBadge>` status colours | M | M | Block on M1 (per-week data must exist before the accordion can show history). Design reference in area-c-ui.md. |
| L6 | Full Fluent UI redesign — `StandingTeeTimes.razor` admin page with `<FluentDataGrid>`, `<FluentTabs>` status filter with count badges, `<FluentDialog>` approve/deny workflows, URL-persisted filter state | M | L | Design reference in area-c-ui.md. Requires Fluent UI package install. |
| L7 | Phone-request / first-call-first-served scheduling phase — the post-standing-allocation phase where remaining slots are offered by phone | L | XL | Out of scope for this review cycle. Flag as a future backlog item once standing allocation is stable. |

---

## Grouped Tasks (must ship together)

| Group | Tasks | Reason |
|-------|-------|--------|
| Immediate crash fixes | C1 + C2 + C3 | All are one-line or two-line changes; ship as a single "quick fixes" PR before any other work |
| Date validation | H5 + M3 + M4 | All add input-level date guards; one service change + one Razor change |
| Audit fields | H2 + M6 | `ApprovedByUserId/Date` and `DenyReason` both require a migration and service signature changes; batch into one migration |
| Priority uniqueness | H3 + H6 | DB constraint and UI conflict warning are two sides of the same fix |
| Week allocation model | M1 + H4 + (M2) | `StandingTeeTimeWeekAllocation` entity creation, remove dead `GeneratedBookings` property, add `CreatedAtUtc`; one migration |
| Allocation engine | L1 + L2 + L3 | Must be sequenced (engine, then trigger, then special-event suppression) |
| Fluent UI | L4 + L5 + L6 | Independent of each other but all require Fluent UI package install; coordinate with Area F shell migration |

## Independent Tasks (can be parallelised)

H1 (Shareholder check), H7 (uniqueness semantics), M5 (tests), M7 (role auth), M8 (admin sort), M9 (cancel allocated), L5 (member redesign, once M1 done)
