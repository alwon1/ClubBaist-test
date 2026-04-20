# Player Scores – Deferred Planning Notes

This document preserves design decisions and considerations discussed during MVP planning that apply to future use cases. It exists so future planning sessions can pick up where this one left off without relitigating decisions already made.

Do not implement anything in this file. These are planning seeds only.

---

## UC-PS-02 — Record Score from External Course

**Actor:** Member

**Key considerations:**
- Requires course name, Golf Canada course ID or approval status, course rating, and slope rating entered manually (these are not in the Club BAIST lookup table)
- MVP `GolfRound` is Club-BAIST-specific; external rounds may need a nullable `CourseName` field or a separate `ExternalGolfRound` entity
- Open design question (Q-F3): Does `GolfRound` get a `CourseSource` discriminator (`ClubBaist` / `External`), or is there a separate `ExternalGolfRound` entity that shares a common base? Decide at planning time for UC-PS-02.

---

## UC-PS-03 — Calculate / Update Handicap Index

**Key considerations:**
- The raw data stored by UC-PS-01 (tee color, 18 hole scores) combined with the Club BAIST course/slope ratings (looked up by tee color and member gender) is exactly what WHS handicap differentials require
- WHS differential formula: `(Adjusted Gross Score − Course Rating) × 113 / Slope Rating`
- Handicap Index uses the best 8 of last 20 differentials
- Course rating and slope rating are **not stored in MVP** — they are looked up at UC-PS-03 time using the stored tee color and the member's gender on file
- No schema changes to `GolfRound` needed when this is implemented — the necessary inputs are already stored
- Source documents: `HandicappingReferenceGuide.pdf`, `Golf-Canada-WHS-Rules-of-Handicap-–-ENG-Final.pdf`
- **CourseRating storage (DECIDED):** Course and slope ratings will be stored in the database (seed table) when UC-PS-03 is implemented. This allows ratings to be updated without a deploy.
- Open design question (Q-F4): WHS Playing Conditions Calculation (PCC) adjustment — is this a field on `GolfRound` (stored at submission time, requiring UC-PS-06 data to exist first) or computed on the fly from a `CourseCondition` record? Decide at UC-PS-03/UC-PS-06 planning time.

---

## UC-PS-04 — View Member Handicap Index Report

**Fields required on report:**
- Date
- Member Name
- Handicap Index
- Last 20 Average
- Best 8 Average
- Last 20 Round Scores

**Key considerations:**
- All fields are derivable from `GolfRound` records once UC-PS-03 is implemented
- No new stored data required beyond UC-PS-03 outputs
- This is a read-only reporting use case; no writes

---

## UC-PS-06 — Record Course Conditions / Weather

**Actor:** Course Committee / Clerk (not Member)

**Purpose:** Record weather and playing conditions for a given date or tee time window. May feed a future WHS Playing Conditions Calculation (PCC) adjustment.

**MVP accommodation strategy:**
- `GolfRound` deliberately has no weather or conditions fields
- A future `CourseCondition` entity will link to a date or `TeeTimeSlotStart` window
- `GolfRound` is joinable to `CourseCondition` via `TeeTimeBooking.TeeTimeSlotStart` (date component) — zero changes to `GolfRound` required when this is implemented

**Open design question (Q-F4):** See UC-PS-03 notes above.

---

## Open Design Questions for Future Sessions

| ID | Question | Relevant UC |
|----|----------|-------------|
| Q-F1 | ~~IScoreEligibilityRule pattern~~ **DECIDED — flat logic in ScoreService; only one eligibility rule will ever exist** | UC-PS-01 implementation |
| Q-F2 | ~~CourseRating lookup~~ **DECIDED — DB seed table** | UC-PS-03 |
| Q-F3 | `GolfRound` `CourseSource` discriminator vs. separate `ExternalGolfRound` entity? | UC-PS-02 |
| Q-F4 | WHS PCC adjustment — stored on `GolfRound` at submission time vs. computed from `CourseCondition` on the fly? | UC-PS-03 / UC-PS-06 |

---

## Removed Use Cases

| UC | Title | Reason |
|----|-------|--------|
| UC-PS-05 | Record Abbreviated (9-Hole) Round | Removed — not planned for the foreseeable future |
