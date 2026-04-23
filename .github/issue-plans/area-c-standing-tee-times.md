# Area C: Standing Tee Times – Design Questions

> **Answer after Areas F and E.**

These decisions shape the standing tee time system: `StandingRequest`, `MyStandingRequests`, and the admin `StandingTeeTimes` page.

**Related review files:** `.review/area-c-gap.md`, `.review/area-c-design.md`, `.review/area-c-ui.md`, `.review/area-c-priority.md`

---

## Question 16 – Standing tee time allocation engine

The weekly **priority-ordered placement engine** that converts approved standing tee time requests into actual tee time bookings is **entirely missing** from the codebase.

Without it, approved standing requests exist in the database but never result in actual booked slots.

Should the allocation engine be **built in this phase**?

- **Option A – Build now (manual admin action):** Implement the engine as a manually triggered admin action: admin clicks "Run weekly allocation" on the `StandingTeeTimes.razor` admin page. Engine iterates approved requests in priority order and creates `TeeTimeBooking` records for the next week's tee sheet.
- **Option B – Build now (automatic background job):** Implement as an `IHostedService` or `BackgroundService` that runs automatically (e.g., every Sunday evening). More hands-off but harder to test and debug.
- **Option C – Defer allocation engine:** The request/approval workflow exists; defer the actual allocation placement to a later phase.

**Your answer:**
<!-- e.g. "Option A – build as manual admin action first, automate later" -->

---

## Question 17 – One-active-request-per-member rule strictness

The current implementation enforces **one active standing tee time request per member** (across all days/times). The business spec implies a softer rule: **one request per week per day-of-week** (a member could have one standing slot on Mondays and a different one on Thursdays).

Should we **relax the rule** or **leave the current stricter logic**?

- **Option A – Relax to one-per-week-per-day-of-week:** Aligns with the spec; requires updating `StandingTeeTimeService` and the validation in `StandingRequest.razor`.
- **Option B – Keep the current strict one-active-request-per-member rule:** Simpler; may not match the business intent but is a known conservative interpretation.

**Your answer:**
<!-- e.g. "Option A – relax to one-per-week-per-day-of-week per the spec" -->
