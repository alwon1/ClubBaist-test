# Area B: Regular Tee Time Bookings — Gap Analysis

## Summary

The system has a solid core: slot generation, a composable rule pipeline (past-slot, special-event blocking, membership-level availability, max-participants, duplicate detection), a `BookingService` with transaction safety, member-facing reservation/cancellation UI, and a staff console. However, several business rules from the spec are absent or incorrectly implemented: the "one week in advance" booking window is entirely missing, the slot interval does not match the spec, the Silver/Bronze membership time windows have boundary defects, holidays are not modelled, Copper (Social) members are not represented at all, the Gold tier seeding is ambiguous, and the Staff Console daily tee sheet is missing required fields. Test coverage for booking rules is limited to unit tests; no integration tests exercise the membership-level time windows or the advance-booking window.

---

## Missing Features

### 1. One-Week Advance Booking Limit
The spec states: "Tee times can be made up to one week in advance." There is no `IBookingRule` that enforces this. `PastSlotRule` rejects past slots; nothing rejects slots more than 7 days in the future. A member can book any slot for any date as long as a `TeeTimeSlot` row exists in the database. The season is seeded to run until October 15, so members effectively have an unlimited forward booking window.

**Files affected:** `/home/user/ClubBaist-test/ClubBaist.Domain2/Booking/Rules/` (missing rule), `/home/user/ClubBaist-test/ClubBaist.Services2/ServiceCollectionExtensions2.cs` (rule not registered).

### 2. Copper (Social) Membership — No Golf Privileges
The spec says Copper/Social members have NO golf privileges. There is no `MembershipLevel` record seeded for Copper/Social, and no rule or guard that blocks a member with this level from booking. The `MembershipLevelAvailabilityRule` would incidentally block them if they had no availability rows, but Copper is not modelled at all.

**File affected:** `/home/user/ClubBaist-test/ClubBaist.Web/Data/AppDbContextSeed.cs` — no Copper level exists.

### 3. Number of Carts — Tee Sheet Field Not Stored
The spec's daily tee sheet requires a "Number of Carts" field per booking. `TeeTimeBooking` has no `NumberOfCarts` property. The create-reservation UI has no cart selector.

**Files affected:** `/home/user/ClubBaist-test/ClubBaist.Domain2/Entities/Booking/TeeTimeBooking.cs`, `/home/user/ClubBaist-test/ClubBaist.Web/Components/Pages/TeeTimes/CreateReservation.razor`.

### 4. Employee Name — Tee Sheet Field Not Stored
The spec lists "Employee Name" as a tee sheet field. Neither `TeeTimeBooking` nor `TeeTimeSlot` stores the employee/clerk who created or processed a booking.

**File affected:** `/home/user/ClubBaist-test/ClubBaist.Domain2/Entities/Booking/TeeTimeBooking.cs`.

### 5. Phone Number on Tee Sheet
The spec lists "Phone" as a tee sheet column. Although `ClubBaistUser` stores `PhoneNumber`, the Staff Console's Reservations grid (`StaffConsole.razor`) does not display the booking member's phone number. The `BookingRow` record (`BookingId`, `SlotStart`, `BookingMemberName`, `MemberNumber`, `PlayerCount`) does not include phone.

**File affected:** `/home/user/ClubBaist-test/ClubBaist.Web/Components/Pages/TeeTimes/StaffConsole.razor`.

### 6. Reprimand / No-Show Tracking
The spec states that members who do not cancel will be "reprimanded appropriately." There is no reprimand, warning, strike, or no-show record in the domain model or any service.

### 7. Special Event Management UI
The spec allows a clerk to create special events that block tee times. `SpecialEvent` is modelled and `SpecialEventBlockingRule` enforces it, but there is no UI for staff to create, edit, or delete special events.

---

## Incorrect Implementations

### 1. Slot Interval Is Not 8 Minutes — It Alternates 7/8 Minutes per 15-Minute Window
`SeasonService2.GenerateSlots` produces two slots per 15-minute window: one starting at offset 0 (duration 7 min) and one at offset 7 (duration 8 min). The resulting pattern is gaps of 7 min, then 8 min, repeating. So slot starts go: 7:00, 7:07, 7:15, 7:22, 7:30 … This matches the sample intervals in the spec ("7-minute and 8-minute gaps — intervals are approximately 8 minutes"). This is therefore a spec-compliant implementation, although the spec calls it "8-minute intervals" in the heading. No defect here, but it is worth noting that two slots share each 15-minute window and each has a different duration, which could surprise operators expecting uniform spacing.

### 2. Silver Weekday Afternoon Window — Start Boundary Is Wrong
The spec says Silver members may play "Before 3:00 PM / After 5:30 PM" on weekdays. The seeded availability for Silver (`"SV"`) is:
- Window 1: `StartTime = 07:00`, `EndTime = 15:00`
- Window 2: `StartTime = 17:30`, `EndTime = 19:00`

The `MembershipLevelAvailabilityRule` uses `a.EndTime >= TimeOnly.FromDateTime(p.Slot.Start)` (inclusive end). This means a slot at exactly 15:00 is **allowed** by window 1, but the spec says "Before 3:00 PM" — 3:00 PM itself should be excluded. The rule should use a strict less-than (`<`) for the end boundary, or the seeded `EndTime` for window 1 should be `14:52` (last valid slot before 3 PM).

**File affected:** `/home/user/ClubBaist-test/ClubBaist.Web/Data/AppDbContextSeed.cs` (lines 114–115), `/home/user/ClubBaist-test/ClubBaist.Domain2/Booking/Rules/MembershipLevelAvailabilityRule.cs` (line 18: `a.EndTime >= ...`).

Note: The UI's `CanBook` method in `TeeTimeAvailabilityPanel.razor` (line 507) uses `timeOfDay < a.EndTime` (strict less-than), making it inconsistent with the rule's `>=`. The UI would exclude slots at exactly `EndTime` while the rule allows them.

### 3. Bronze Weekday Morning Window Start Time
The spec says Bronze members may play "Before 3:00 PM / After 6:00 PM" on weekdays. The seeded start time for Bronze window 1 is `07:00`, which is correct. However, the same inclusive-end defect described above applies: a slot at exactly 15:00 is incorrectly allowed.

### 4. Gold Membership Not Seeded — Shareholder/Associate Treated as Gold
The spec defines four tiers: Gold, Silver, Bronze, Copper. The seed data has: Shareholder (SH), Silver (SV), Bronze (BR), Associate (AS). There is no "Gold" level. The `AddAvailabilities` method's `default` case (covering SH, AS, and any unknown code) grants full 07:00–19:00 access every day, which matches Gold's spec. However, this mapping is implicit — "Shareholder" and "Associate" are treated identically to Gold with no documentation connecting them. If a truly distinct "Gold" level is needed, or if Shareholder/Associate should have different rules, the current seeding silently gives both full access.

### 5. Booking Window Upper Bound Uses Season Slots — Not Spec's "One Week" Rule
`TeeTimeAvailabilityPanel` shows all slots in the current season. The date picker has no upper-bound validation. There is no guard preventing a member from selecting a date 3 months in the future. This is a direct gap relative to the spec.

---

## Booking Rules Analysis

| Rule | Spec Requirement | Implementation | Status |
|---|---|---|---|
| Past slot | Cannot book in the past | `PastSlotRule` rejects any slot with `Start < DateTime.Now` | Correct |
| One week in advance | Bookings no more than 7 days ahead | No rule implemented | **MISSING** |
| Special event blocking | Clerk crosses off blocked times | `SpecialEventBlockingRule` filters slots within event range | Correct |
| Membership level access | Per-level time windows (see next section) | `MembershipLevelAvailabilityRule` + seeded availabilities | Partially correct (boundary defect) |
| Max 4 participants | 1 to 4 golfers per tee time | `MaxParticipantsRule` enforces max 4 | Correct |
| No duplicate participants | Same person cannot appear twice | `DuplicateBookingRule` (2-hour conflict window, cross-member check) | Correct for same-slot; conflict window is 2 hours, not a per-day limit — a member could book two slots the same day if they are >2 hours apart |
| Cancellation | Member must cancel or be reprimanded | Cancel workflow exists in `BookingService.CancelBookingAsync` and UI | Partially correct — reprimand/no-show tracking is absent |
| Slot interval | ~8-minute intervals | Alternating 7/8-min slots inside 15-min windows | Matches sample data |

---

## Membership Level Access Window Analysis

### Spec Requirements

| Membership | Mon–Fri | Sat/Sun/Holiday |
|---|---|---|
| Gold | Anytime (07:00–19:00) | Anytime (07:00–19:00) |
| Silver | Before 3:00 PM AND After 5:30 PM | After 11:00 AM |
| Bronze | Before 3:00 PM AND After 6:00 PM | After 1:00 PM |
| Copper | No golf privileges | No golf privileges |

### Implementation (from `AppDbContextSeed.cs`)

| Level | Weekday Window 1 | Weekday Window 2 | Weekend |
|---|---|---|---|
| SH (Shareholder/Gold) | 07:00–19:00 | — | 07:00–19:00 |
| SV (Silver) | 07:00–15:00 | 17:30–19:00 | 11:00–19:00 |
| BR (Bronze) | 07:00–15:00 | 18:00–19:00 | 13:00–19:00 |
| AS (Associate/Gold) | 07:00–19:00 | — | 07:00–19:00 |
| Copper | Not modelled | — | Not modelled |

### Defects

1. **Silver weekend start — correct:** 11:00 AM matches "After 11:00 AM." However the rule's inclusive `EndTime >=` means a slot at exactly 11:00 is allowed, which aligns with "After 11:00 AM" only if "After" is interpreted as "at or after." This is a language ambiguity; the implementation is defensible.

2. **Silver weekday afternoon end boundary — incorrect:** `EndTime = 15:00` with an inclusive rule (`>=`) allows a slot at exactly 15:00. The spec says "Before 3:00 PM" which should exclude 15:00 exactly. The UI `CanBook` check uses `< a.EndTime` (strict), creating an inconsistency between display logic and booking enforcement.

3. **Bronze weekday afternoon end boundary — same defect as Silver.**

4. **Holiday detection — completely absent:** The spec distinguishes weekdays from "Weekend/Holiday." Public holidays (e.g., Victoria Day, Canada Day) fall on weekdays but should use the weekend/holiday rules. There is no `PublicHoliday` entity, no holiday calendar, and no logic in `MembershipLevelAvailabilityRule` or slot generation to treat a holiday weekday as a weekend for access purposes.

5. **Copper/Social — not modelled:** No `MembershipLevel` row exists for Copper. The system cannot represent a Copper member.

---

## Edge Cases Not Handled

1. **Exactly 3:00 PM for Silver/Bronze on a weekday:** Due to the inclusive `EndTime >=` in the rule and `EndTime = 15:00`, a slot at exactly 15:00 is permitted by the rule but blocked by the UI's `CanBook` check. A booking submitted via the API or service layer directly would succeed; one attempted through the UI would appear restricted. This split behaviour is a latent bug.

2. **Holiday weekdays:** A Silver member on a Monday that is a public holiday should be restricted to "After 11:00 AM," but the implementation would apply weekday rules instead (Before 3 PM / After 5:30 PM).

3. **Season boundary — booking into next season:** The current season is seeded; if a new season is created by staff, a member could book into future-season slots with no restriction other than the non-existent advance-booking rule.

4. **Minimum participants — booking with 0 golfers:** The spec says "1 to 4 golfers." `TeeTimeBooking` always has at least 1 (the `BookingMember`), so the lower bound is structurally enforced. However, `UpdateBookingAsync` allows removing all `AdditionalParticipants` leaving only the booking member, which is valid. No defect here, but worth noting.

5. **Slot at operating-hours close (19:00):** `SeasonService2.GenerateSlots` stops generating once `slotStart >= close` (19:00). The last slot before 19:00 will be the 18:52 slot (7-minute duration ending at 18:59). Slots that start at 19:00 are never generated, so no slots fall exactly on closing time. This is correct.

6. **Concurrent bookings — same slot:** `BookingService.CreateBookingAsync` uses Snapshot isolation and a DB-level unique index on `(TeeTimeSlotStart, BookingMemberId)` to prevent duplicate primary-member bookings for the same slot. However, if two members simultaneously try to claim the last spot, the `MaxParticipantsRule` check and the save are not atomic at the application level — only the DB constraint and Snapshot isolation provide the safeguard. This should be verified against actual concurrency behaviour.

7. **CreateReservation page does not re-validate membership level access:** When `isAdmin = true`, the admin can select any member as the booking member and submit without the membership-level rule being checked in the UI layer. The rule IS enforced server-side by `BookingService`, so the booking would be rejected, but the error message is generic ("Unable to create booking. The slot may be full or booking rules prevent this booking.") rather than explanatory.

---

## UI Issues

### Staff Console — Missing Tee Sheet Fields
The Reservations grid in `StaffConsole.razor` shows: Date, Time, Booked By, Member #, Players, Status, Actions.

Missing fields required by the spec:
- **Phone** — booking member's phone number is not displayed
- **Number of Carts** — not stored, not displayed
- **Employee Name** — not stored, not displayed
- **Day of Week** — the date column shows "MMM d, yyyy" but not the day name; the spec lists "Day of Week" as a separate column

### CreateReservation — No Phone or Cart Fields
The booking form collects only date/time, booking member, and additional players. No cart count or phone confirmation step.

### CreateReservation — Generic Error on Rule Rejection
When `BookingService.CreateBookingAsync` returns `false`, the UI shows: "Unable to create booking. The slot may be full or booking rules prevent this booking." The actual rejection reason (e.g., "Not available to Silver members at this time") is logged server-side but never surfaced to the user.

**File affected:** `/home/user/ClubBaist-test/ClubBaist.Web/Components/Pages/TeeTimes/CreateReservation.razor` (line 303–305).

### Availability Panel — No Advance-Booking Date Limit
`TeeTimeAvailabilityPanel` defaults to today and allows navigating to any future date. There is no UI-level guard limiting date selection to within 7 days, matching the missing advance-booking rule.

### MyReservations — Players Column Shows Count Only
The spec says members want to "see names of all players scheduled to play." The `MyReservations.razor` grid shows only the count in the Players column. Full names are only visible in `ReservationDetail.razor` for your own reservation. The public availability view (`TeeTimeAvailabilityPanel`) does show first names of all booked players as badges, which partially satisfies this requirement.

### ReservationDetail — Edit Restricted to Past Bookings Hidden But Cancel Not
When a booking is in the past (`TeeTimeSlotStart < DateTime.Now`), the Edit Players button and Cancel button are both hidden, which is correct. However, the past reservation's player list is not shown at all in the past-booking view — only the count is visible in `MyReservations`, and `ReservationDetail` shows nothing for past bookings in the "else" branch (the Players card is only rendered when `booking.TeeTimeSlotStart >= DateTime.Now`).

**File affected:** `/home/user/ClubBaist-test/ClubBaist.Web/Components/Pages/TeeTimes/ReservationDetail.razor` (lines 133–154 — player list conditional is tied to `>= DateTime.Now`).

---

## Test Coverage Gaps

### What Is Tested
- `MaxParticipantsRule` — thoroughly covered in `Test1.cs` (6 tests)
- `DuplicateBookingRule` — thoroughly covered (11 tests including window boundary)
- `MembershipLevelAvailabilityRule` — covered (7 tests including boundary semantics)
- `SpecialEventBlockingRule` — covered (7 tests including exact start/end semantics)
- `PastSlotRule` — covered (4 tests)
- `BookingRuleExtensions` — covered (5 tests)
- `BookingService.CreateBookingAsync` — one integration test (out-of-season slot rejection)
- `BookingService.CancelBookingAsync` — one integration test
- `BookingService.UpdateBookingAsync` — two integration tests (replace participants, conflict rejection)

### What Is Not Tested

1. **One-week advance booking rule** — no such rule exists, so no tests exist.

2. **Silver/Bronze actual time-window values** — no test verifies that the seeded `MembershipLevelTeeTimeAvailability` rows produce the correct allow/reject outcomes for Silver at 14:59 (allow), 15:00 (should reject per spec), 15:01 (reject), 17:29 (reject), 17:30 (allow). The boundary at exactly 15:00 is never tested.

3. **Holiday detection** — no test exists (feature is absent).

4. **Copper/Social member blocked entirely** — no test verifies that a Copper member cannot book (level not seeded).

5. **Full membership-level integration** — the integration `BookingServiceTests.CreateBooking_OutsideSeason_ReturnsFalse` verifies past-slot rejection indirectly, but no integration test seeds Silver/Bronze availability rows and confirms that a Silver member is rejected at 15:00 but accepted at 9:00.

6. **Concurrent booking** — no test validates that two simultaneous requests for the last spot in a slot result in exactly one success.

7. **UpdateBookingAsync with membership-level violation** — no test confirms that updating participants is rejected if the booking member's level is no longer allowed at that time (e.g., Bronze at a Gold-only hour).

8. **Staff Console display fields** — no UI test or snapshot test verifies that the tee sheet columns match spec requirements.

9. **`SeasonService2.GenerateSlots` slot interval** — no test asserts the specific alternating 7/8-minute pattern or that the spec's sample times (7:00, 7:07, 7:15, 7:22…) are generated correctly.

---

## Notes

- **`MembershipLevelTeeTimeAvailability` entity** is defined in both `MembershipLevel.cs` (inside `/Entities/Membership/`) and referenced from the same namespace in `ClubBaist.Domain2`. The actual class lives in `/home/user/ClubBaist-test/ClubBaist.Domain2/Entities/Membership/MembershipLevel.cs` — it is not in a separate file despite the brief listing suggesting `MembershipLevelTeeTimeAvailability.cs`.

- **`CanBook` vs. `MembershipLevelAvailabilityRule` boundary inconsistency:** `TeeTimeAvailabilityPanel.CanBook` (line 507) uses `timeOfDay < a.EndTime` (exclusive), but `MembershipLevelAvailabilityRule` (line 18) uses `a.EndTime >= TimeOnly.FromDateTime(...)` (inclusive). A slot at exactly `EndTime` will show as "restricted" in the UI but pass the server-side rule. This is the most significant consistency defect.

- **`TestInfrastructure.CreateMembershipLevelAsync`** seeds availabilities with a single window (`openingHour`–`closingHour`) for all 7 days. This correctly omits Silver/Bronze windowing for test cases that do not need it, but it means no test exercises the real seeded availability configuration.

- The `BookingService.EvaluateBookingAsync` private method is tested via reflection in `BookingPerformanceTests.cs`, which is marked `[Ignore]` for CI. This is an acceptable trade-off for a performance test but makes the internal evaluation path invisible in normal test runs.
