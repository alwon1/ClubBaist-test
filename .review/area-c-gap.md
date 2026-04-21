# Area C: Standing Tee Times — Gap Analysis

## Summary

The core data model, request submission flow, and admin approve/deny/cancel operations are implemented and well-tested. However, the system is missing the entire allocation engine (no weekly tee-sheet generation, no priority-ordered placement against a tee sheet, no special-event conflict handling), the "Approved By / Approved Date" audit fields required by the business card, and the Shareholder-role enforcement on who may submit a request. The implementation covers roughly the intake and approval phases but stops short of the allocation phase that is central to the business process.

---

## Missing Features

1. **Allocation Engine — no tee-sheet generation or priority placement** (High)
   The business requires a clerk to run a weekly allocation pass that fills approved standing requests onto the tee sheet by priority number, marks each as Allocated or Unallocated, and generates the actual bookings. No such service method, scheduled job, or UI workflow exists. `StandingTeeTimeStatus.Allocated` and `Unallocated` are defined in the enum but are never set anywhere in the codebase.

2. **Special-Event Conflict Handling** (High)
   The business requires special events to take precedence over standing tee times, with affected slots crossed off before standing requests are processed. There is no special-event concept, no conflict-checking logic, and no mechanism to suppress or Unallocate a standing request when a special event occupies its slot.

3. **Approved By / Approved Date fields missing from domain model** (High)
   The business card requires recording who approved the request and when. `StandingTeeTime.cs` has `ApprovedTime` (the tee time, not the timestamp) but has no `ApprovedBy` (staff identity) or `ApprovedDate` (approval timestamp) properties. The `ApproveAsync` service method accepts no actor identity parameter and persists none.

4. **Shareholder-Only Access Not Enforced** (High)
   The business rule states only SHAREHOLDER members may submit a standing tee time request. `StandingRequest.razor` is gated by the `BookStandingTeeTime` permission policy (a claim-based check), but `SubmitRequestAsync` in the service performs no membership-level verification. A user who has the permission but is not actually a Shareholder can submit successfully. The member's `MembershipLevel` is loaded on the page but never validated before submission.

5. **One Standing Request Per Member Per Week Not Enforced** (Medium)
   The business rule is "one standing tee time request per shareholder per week." The service enforces only one active request total (any status other than Cancelled/Denied), which is stricter than "per week" but does not match the stated requirement. The per-week scoping (e.g., by `RequestedDayOfWeek` within a date range) is absent.

6. **Tee-Sheet Created One Week in Advance — No Lead-Time Validation** (Medium)
   The business process requires the tee sheet to be created one week in advance. There is no validation that `StartDate` is at least one week in the future, nor any scheduling or calendar concept for when allocation runs.

7. **Phone-Request / First-Call-First-Served Scheduling** (Low)
   After standing requests are placed, phone requests are scheduled first-call-first-served. This post-standing booking phase is entirely absent (no queue, no UI, no service logic). It is a separate concern but is part of the same tee-sheet workflow.

---

## Incorrect Implementations

- **Tolerance range in UI does not match business rule.** `StandingRequest.razor` offers tolerance options of 0, 15, 30, and 60 minutes. The business specifies ±30 minutes as the tolerance; ±60 minutes is not a valid business option and should not be selectable. The domain model also allows up to 120 minutes (`[Range(0, 120)]`) with no business justification.

- **"One active request" check uses wrong semantics.** `SubmitRequestAsync` blocks a second submission if any non-Cancelled/non-Denied request exists, regardless of day of week or date range. This prevents a member from having overlapping standing requests for different days, which may be overly restrictive or inconsistently restrictive depending on the true intent of "one per week."

- **Admin page restricted to `Admin` role only.** `/admin/standing-teetimes` uses `[Authorize(Roles = AppRoles.Admin)]`. The business process involves pro-shop clerks, not necessarily system administrators. There is no `Clerk` or `ProShop` role defined in `AppRoles`. If clerks are intended to be a distinct role, the admin page is incorrectly restricted.

- **`ApproveAsync` default time in admin UI is always 08:00.** `OpenApprovePanel` hardcodes `approvedTimeStr = "08:00"` instead of defaulting to the member's requested time. A clerk must always manually change the field, increasing the chance of approving the wrong time.

---

## Status Workflow Analysis

Defined statuses: `Draft → Approved → Allocated / Unallocated`, and from Draft: `Denied`; from any active state: `Cancelled`.

| Transition | Implemented | Notes |
|---|---|---|
| (new) → Draft | Yes | `SubmitRequestAsync` |
| Draft → Approved | Yes | `ApproveAsync` |
| Draft → Denied | Yes | `DenyAsync` |
| Approved → Allocated | **No** | No allocation engine |
| Approved → Unallocated | **No** | No allocation engine |
| Any active → Cancelled | Yes | `CancelAsync` (member-initiated) |
| Allocated → (re-run next week) | **No** | No weekly recurrence logic |

The `Allocated` and `Unallocated` states exist in the enum but are unreachable at runtime.

---

## Allocation Logic Analysis

No allocation logic is implemented. The business process requires:

1. Load approved standing requests ordered by `PriorityNumber`.
2. For each request, find an available tee-time slot on the correct `RequestedDayOfWeek` within `ToleranceMinutes` of `ApprovedTime`.
3. Skip slots blocked by special events.
4. Create a `TeeTimeBooking` linked via `StandingTeeTimeId` and transition the standing request to `Allocated`.
5. If no slot is available, transition to `Unallocated`.

None of these steps have a corresponding service method, scheduled task, or admin action. The `GeneratedBookings` property on the entity (`[NotMapped]`) and the `StandingTeeTimeId` FK on `TeeTimeBooking` (seen in test infrastructure) confirm the data model anticipates this link, but no code populates it outside of the test setup helper.

---

## Weekly Recurrence Analysis

A standing tee time is described as recurring each week for the duration `StartDate` to `EndDate`. The current implementation stores one `StandingTeeTime` record covering the full date range but has no mechanism to:

- Trigger a booking generation run each week.
- Track which weeks have been allocated vs. skipped.
- Handle a week where the slot was unavailable (back-fill or leave as Unallocated for that week only).
- Expire the standing request when `EndDate` passes.

The `Status` field is a single value — it cannot represent "allocated on some weeks, unallocated on others." A week-level allocation record (e.g., `StandingTeeTimeWeekAllocation`) or a per-booking status log would be needed to support true weekly recurrence tracking.

---

## Edge Cases Not Handled

- **Start date in the past:** No validation prevents submitting a standing request with a `StartDate` before today.
- **Start date equals end date:** `EndDate <= StartDate` is rejected, but `EndDate == StartDate.AddDays(1)` (a one-day window that does not include the requested day of week) is accepted.
- **Requested day of week never falls in date range:** No check confirms the `RequestedDayOfWeek` occurs at least once between `StartDate` and `EndDate`.
- **Priority number uniqueness:** Two requests can be assigned the same `PriorityNumber`. The allocation process requires a strict ordering; ties are undefined.
- **Cancelling an Allocated request:** `CancelAsync` allows cancellation of an `Allocated` standing request, but there is no logic to also cancel or flag the already-generated bookings for that week.
- **Member number display for additional players:** The member-selection dropdown in `StandingRequest.razor` loads all members from the database with no membership-level filter. Non-shareholder members are selectable as participants, which may or may not be a business constraint, but is undocumented and unchecked.

---

## UI Issues

- **Admin page has no sort-by-priority view.** The business process works through requests in priority order; the admin grid offers sorting by status, day, and member name but no dedicated "sort by priority ascending" default, making the allocation workflow awkward.
- **Admin page shows no member number for the booking member.** The Players column in the admin grid shows first/last name for the booking member but omits the membership number, unlike the Member column which does show it. Inconsistent display.
- **Approve panel does not pre-populate from requested time.** As noted above, the approved-time input defaults to 08:00 rather than the member's `RequestedTime`, forcing the clerk to retype it.
- **`MyStandingRequests.razor` loads `BookingMember` navigation but `GetForMemberAsync` uses `AsNoTracking` with no `Include`.** The `BookingMember` and `AdditionalParticipants` navigations will be null/empty when rendered, causing a null-reference exception on `@context.BookingMember.User.FirstName` unless EF lazy loading is configured globally.
- **`GetAllAsync` likewise has no `Include` for navigation properties,** so the admin grid will also fail to render names and participants unless lazy loading is enabled — this is not verified in the files reviewed.

---

## Test Coverage Gaps

**Covered:**
- `SubmitRequestAsync`: valid foursome, second active request, end-before-start, fewer than 3 players, booking member in participants, duplicate participants.
- `ApproveAsync`: happy path, double-approve, invalid priority number.
- `DenyAsync`: happy path, deny non-draft.
- `CancelAsync`: own request, wrong member, already-cancelled.
- Entity persistence and booking link (phase one integration test).

**Not covered:**
- Shareholder-role enforcement (no test verifies a non-shareholder cannot submit).
- `ApproveAsync` with no priority number (null) — happy path is tested with `priorityNumber: 2` but not `null`.
- Allocation logic (no tests exist because the feature does not exist).
- Weekly recurrence (no tests).
- Special-event conflict suppression (no tests).
- `StartDate` in the past validation (no test; validation is also absent in code).
- Requested day of week not in date range (no test; validation absent).
- Priority number conflict / duplicate detection (no test; validation absent).
- Cancel of an Allocated request leaving orphan bookings.
- Navigation property loading (no test confirms names render correctly in list pages).

---

## Notes

- `AppRoles.Permissions.BookStandingTeeTime` and `AppRoles.Claims.StandingTeeTimeBooking` are well-structured for claim-based access but the mechanism by which a Shareholder member actually receives this claim (e.g., on login, from a policy, from a seeded role) is not visible in the reviewed files and should be verified.
- The `[NotMapped] GeneratedBookings` property uses a C# 13 `field` keyword semi-auto property (`field ??= new()`). This is modern syntax that may cause compile errors on older SDK versions; worth flagging for build-environment compatibility.
- The business card model lists four member number/name slots (booking member + 3 additional). The domain model matches this exactly (`BookingMember` + `AdditionalParticipants` max 3), which is correct.
