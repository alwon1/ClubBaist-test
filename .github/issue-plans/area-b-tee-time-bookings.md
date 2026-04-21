# Area B: Regular Tee Time Bookings – Design Questions

> **Answer after Areas F and E.**

These decisions shape the tee time booking system: `Availability` panel, `CreateReservation`, `StaffConsole`, `MyReservations`, `ReservationDetail`.

**Related review files:** `.review/area-b-gap.md`, `.review/area-b-design.md`, `.review/area-b-ui.md`, `.review/area-b-priority.md`

---

## Question 12 – Copper (Social) membership

Should **Copper (Social) membership** be implemented in this phase?

This requires:
- Adding the `CP` membership level to seed data
- Seeding a `copper@clubbaist.com` test user
- Enforcing the "no golf privilege" rule (Copper members cannot book tee times; currently this would be an accidental side-effect of having no `MembershipLevelTeeTimeAvailability` rows, but it should be explicit)

- **Option A – Implement Copper now:** Adds the level, seeds a user, adds explicit enforcement (either a new `IBookingRule` or an explicit check in `Availability.razor`).
- **Option B – Defer Copper:** Out of scope for this phase; remain with SH, SV, BR, AS only.

**Your answer:**
<!-- e.g. "Option B – defer Copper to a later phase" -->

---

## Question 13 – Gold/Silver/Bronze tier naming

The spec references **Gold** as the top membership tier, but the implementation uses **Shareholder (SH)** and **Associate (AS)** for full-access tiers.

Should we:
- **Option A – Add a Gold tier concept:** Introduce a `Gold` membership level that maps to the SH/AS full-access behavior; may cause confusion with the existing SH/AS levels.
- **Option B – Leave the current SH/AS-as-Gold approach:** Keep `Shareholder` and `Associate` as the full-access tiers; update documentation to clarify they correspond to "Gold" in business terminology.
- **Option C – Rename SH → Gold:** Rename the Shareholder level to Gold in the data model and UI; keep SH as the short code for backward compatibility.

**Your answer:**
<!-- e.g. "Option B – keep SH/AS approach, update docs" -->

---

## Question 14 – 7-day advance booking rule

The business spec requires that members **cannot book more than 7 days in advance**. This rule is **entirely missing** from the booking pipeline.

Should the **7-day advance booking rule be added now** as a new `IBookingRule` implementation?

- **Option A – Add now:** Implement `AdvanceBookingRule` (or similar) that rejects slots more than 7 days from today; add test coverage.
- **Option B – Defer:** The rule is missing but does not yet cause visible production issues; defer to a later phase.

**Your answer:**
<!-- e.g. "Option A – add the 7-day rule now" -->

---

## Question 15 – StaffConsole tee sheet missing columns

The StaffConsole tee sheet is missing four columns that the business spec requires:

| Column | Schema change required? | Notes |
|---|---|---|
| **Phone** | No — requires JOIN to `ClubBaistUser` | Display booking member's phone number |
| **Number of Carts** | Yes — new column on `TeeTimeBooking` | Track carts requested per booking |
| **Employee Name** | Yes — new column on `TeeTimeBooking` | Record which staff member processed the booking |
| **Day of Week** | No — derived from `TeeTimeSlotStart` | Already in the datetime; just format and display |

Should **all four columns be added** to the StaffConsole tee sheet?

- **Option A – Add all four:** Most complete implementation; requires two schema changes (carts + employee name).
- **Option B – Add Phone and Day of Week only:** No schema change required; display-only additions.
- **Option C – Defer all:** Staff console column additions are lower priority; defer to a later phase.

**Your answer:**
<!-- e.g. "Option B – add Phone and Day of Week now, schema changes in a later PR" -->
