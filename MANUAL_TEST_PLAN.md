# ClubBaist – Manual Test Plan

## Prerequisites
- Application is running locally or deployed (default: `https://localhost:<port>`)
- Database seeded (run migrations + seed; all accounts below use password `Pass@word1`)
- A current golf season exists with tee time slots generated (create one via Admin > Season
  Management if not present)
- Browser: Chrome or Edge (latest)

> **Note on tee time restrictions:** The default seed configures **all** membership levels with
> full 7:00 AM – 7:00 PM availability every day. TC-TEE-002, TC-TEE-007, and TC-TEE-011 test
> time-restriction enforcement and therefore require a one-time setup step (described in each
> case) to narrow Silver and Bronze availability windows before running those tests.

---

## Seed Accounts

| Email | Password | Role | Name | Membership |
|---|---|---|---|---|
| admin@clubbaist.com | Pass@word1 | Admin | Seed Admin | — |
| committee@clubbaist.com | Pass@word1 | MembershipCommittee | Seed Committee | — |
| shareholder1@clubbaist.com | Pass@word1 | Member | Alice Shareholder | Gold/Shareholder |
| shareholder2@clubbaist.com | Pass@word1 | Member | Bob Shareholder | Gold/Shareholder |
| shareholder3@clubbaist.com | Pass@word1 | Member | Carol Shareholder | Gold/Shareholder |
| silver@clubbaist.com | Pass@word1 | Member | Diana Silver | Silver |
| bronze@clubbaist.com | Pass@word1 | Member | Evan Bronze | Bronze |

**Pre-seeded pending applications (already in the database after seed):**

| Email | Name | Status | Requested Level |
|---|---|---|---|
| frank.pending@example.com | Frank Pending | Submitted | Associate |
| grace.onhold@example.com | Grace OnHold | OnHold | Silver |
| henry.waitlist@example.com | Henry Waitlist | Waitlisted | Associate |
| iris.submitted@example.com | Iris Submitted | Submitted | Bronze |
| jack.waitlist@example.com | Jack Waitlist | Waitlisted | Silver |

**New Applicant test data (used in TC-MEM-003):**

| Field | Value |
|---|---|
| First Name | Sam |
| Last Name | Tester |
| Email | sam.tester@example.com |
| Phone | (587) 555-9876 |
| Alternate Phone | (587) 555-0000 |
| Date of Birth | 1988-05-12 |
| Address | 456 Birdie Ave |
| City | Calgary |
| Province | AB |
| Postal Code | T3A 2B5 |
| Occupation | Software Engineer |
| Company Name | Acme Corp |
| Requested Membership Level | Associate |
| Sponsor 1 | Alice Shareholder (shareholder1@clubbaist.com) — look up Member ID from admin panel |
| Sponsor 2 | Bob Shareholder (shareholder2@clubbaist.com) — look up Member ID from admin panel |

---

## Membership Levels & Tee Time Access Rules

| Level | Short Code | Fee | Tee Time Access |
|---|---|---|---|
| Gold/Shareholder | SH | $3,000 | Anytime (7 AM – 7 PM). Can request standing tee times. |
| Associate | AS | $4,500 | Anytime (7 AM – 7 PM) |
| Silver | SV | $2,500 | Anytime (7 AM – 7 PM) — see note below |
| Bronze | BR | $1,000 | Anytime (7 AM – 7 PM) — see note below |

> **Note on time restrictions (TC-TEE-002, TC-TEE-007, TC-TEE-011):** The default seed gives every
> membership level full 7 AM – 7 PM access on all days. To validate the time-restriction scenarios
> you must first update the Silver and Bronze `MembershipLevelTeeTimeAvailability` rows in the
> database so that their allowed windows match the business rules (e.g. Silver before 3 PM / after
> 5:30 PM; Bronze before 3 PM / after 6 PM), or use the Admin UI when that feature is available.
> Skip TC-TEE-002, TC-TEE-007, and TC-TEE-011 if the availability rows have not been configured.

---

## TC-AUTH: Authentication

### TC-AUTH-001 – Successful login
**Preconditions:** Not logged in.  
**Steps:**
1. Navigate to `/Account/Login`
2. Enter Email: `admin@clubbaist.com`, Password: `Pass@word1`
3. Click **Log in**

**Expected Result:** Redirected to home/dashboard. User name "Seed Admin" visible in the navigation bar.

---

### TC-AUTH-002 – Failed login (wrong password)
**Preconditions:** Not logged in.  
**Steps:**
1. Navigate to `/Account/Login`
2. Enter Email: `admin@clubbaist.com`, Password: `wrongpassword`
3. Click **Log in**

**Expected Result:** Error message "Invalid login attempt." displayed. User remains on the login page.

---

### TC-AUTH-003 – Register a new account
**Preconditions:** Not logged in.  
**Steps:**
1. Navigate to `/Account/Register`
2. Enter Email: `newuser.test@example.com`, Password: `Pass@word1`, Confirm Password: `Pass@word1`
3. Click **Register**

**Expected Result:** Account created successfully. User is logged in or redirected with no error shown.

---

### TC-AUTH-004 – Register with mismatched passwords
**Preconditions:** Not logged in.  
**Steps:**
1. Navigate to `/Account/Register`
2. Enter Email: `newuser2@example.com`, Password: `Pass@word1`, Confirm Password: `DifferentPass1`
3. Click **Register**

**Expected Result:** Validation error shown ("Passwords do not match" or equivalent). Form is not submitted.

---

### TC-AUTH-005 – Role-based access: Committee cannot access Admin pages
**Preconditions:** Logged in as `committee@clubbaist.com` / `Pass@word1`.  
**Steps:**
1. Navigate directly to `/admin/users`

**Expected Result:** Access denied page or redirect to home. Admin page content is not displayed.

---

### TC-AUTH-006 – Role-based access: Member cannot access Committee inbox
**Preconditions:** Logged in as `shareholder1@clubbaist.com` / `Pass@word1`.  
**Steps:**
1. Navigate directly to `/membership/applications`

**Expected Result:** Access denied or redirect. Application inbox is not shown.

---

## TC-MEM: Membership Applications

### TC-MEM-001 – View pre-existing applications (Committee)
**Preconditions:** Logged in as `committee@clubbaist.com`.  
**Steps:**
1. Navigate to `/membership/applications`

**Expected Result:** Inbox shows at minimum Frank Pending and Iris Submitted with status **Submitted**. Grace OnHold, Henry Waitlist, and Jack Waitlist appear when filtering by their respective statuses.

---

### TC-MEM-002 – Review and approve an existing application
**Preconditions:** Logged in as `committee@clubbaist.com`. Frank Pending's application is in **Submitted** status.  
**Steps:**
1. Navigate to `/membership/applications`
2. Click on **Frank Pending**'s application
3. Review all displayed fields: name, email, DOB, occupation, company, sponsors, requested level
4. Confirm the requested level is **Associate**
5. Click **Accept / Approve**

**Expected Result:**
- Application status changes to **Accepted**
- A new member account is created for `frank.pending@example.com`
- Logging in as `frank.pending@example.com` with password `ChangeMe123!` succeeds
- Frank's role is **Member** with **Associate** membership level

---

### TC-MEM-003 – Submit a new membership application
**Preconditions:** Logged in as any member (e.g., `shareholder3@clubbaist.com`). Obtain Alice Shareholder's and Bob Shareholder's Member IDs from the admin panel beforehand.  
**Steps:**
1. Navigate to `/membership/apply`
2. Fill in all fields using the **New Applicant test data** table above
3. Enter Alice Shareholder's Member ID in **Sponsor 1**
4. Enter Bob Shareholder's Member ID in **Sponsor 2**
5. Click **Submit**

**Expected Result:** Success message shown. Application appears in the committee inbox at `/membership/applications` with status **Submitted** and applicant name "Sam Tester".

---

### TC-MEM-004 – Submit duplicate application (same email)
**Preconditions:** TC-MEM-003 completed — `sam.tester@example.com` already has an active application.  
**Steps:**
1. Navigate to `/membership/apply`
2. Enter Email: `sam.tester@example.com` and fill in all other required fields
3. Click **Submit**

**Expected Result:** Error "An active application already exists for this email" (or equivalent). No new application created.

---

### TC-MEM-005 – Submit application with invalid postal code
**Preconditions:** Logged in.  
**Steps:**
1. Navigate to `/membership/apply`
2. Fill all fields correctly except Postal Code — enter `12345` (US format)
3. Click **Submit**

**Expected Result:** Validation error on the Postal Code field. Form is not submitted.

---

### TC-MEM-006 – Submit application with invalid phone number
**Preconditions:** Logged in.  
**Steps:**
1. Navigate to `/membership/apply`
2. Fill all fields correctly except Phone — enter `123` (too short)
3. Click **Submit**

**Expected Result:** Validation error on the Phone field. Form is not submitted.

---

### TC-MEM-007 – Submit application with same sponsor twice
**Preconditions:** Logged in.  
**Steps:**
1. Navigate to `/membership/apply`
2. Fill all required fields
3. Set **Sponsor 1** and **Sponsor 2** to the same member ID
4. Click **Submit**

**Expected Result:** Validation error "Sponsors must be different members" (or equivalent). Form is not submitted.

---

### TC-MEM-008 – Deny an application
**Preconditions:** Logged in as `committee@clubbaist.com`. Iris Submitted's application is in **Submitted** status.  
**Steps:**
1. Navigate to `/membership/applications`
2. Open **Iris Submitted**'s application
3. Click **Deny**

**Expected Result:**
- Application status changes to **Denied**
- No user account is created for `iris.submitted@example.com`
- Application no longer appears in the active/submitted queue

---

### TC-MEM-009 – Change application status to OnHold
**Preconditions:** Logged in as `committee@clubbaist.com`. A Submitted application exists.  
**Steps:**
1. Open a **Submitted** application (e.g., any remaining Submitted app)
2. Change the status to **OnHold**
3. Save / Confirm

**Expected Result:** Status updates to **OnHold**. Application reappears under the OnHold filter in the inbox.

---

### TC-MEM-010 – Accepted application is in a terminal state (cannot re-process)
**Preconditions:** TC-MEM-002 completed — Frank Pending's application is **Accepted**.  
**Steps:**
1. Navigate to Frank Pending's application (via admin or committee view)
2. Attempt to click **Deny** or change the status

**Expected Result:** Deny/status-change actions are disabled or not present. The application remains **Accepted**.

---

## TC-TEE: Tee Time Reservations

### TC-TEE-001 – View tee time availability (Shareholder – unrestricted)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. A season with generated slots exists.  
**Steps:**
1. Navigate to `/teetimes`
2. Select any date within the active season

**Expected Result:** Full range of slots (7 AM – 7 PM) displayed as available. No time restrictions applied.

---

### TC-TEE-002 – View tee time availability (Silver member – restricted hours)
**Preconditions:** Logged in as `silver@clubbaist.com`.

> **Setup required:** The default seed gives all levels full 7 AM – 7 PM access. Before running
> this test, log in as `admin@clubbaist.com`, navigate to the membership level settings, and
> restrict the **Silver** level to: before 3:00 PM and after 5:30 PM (remove or narrow the
> availability window for the 3:00 PM – 5:30 PM block). Then log back in as
> `silver@clubbaist.com`.

**Steps:**
1. Navigate to `/teetimes`
2. Select any date within the active season
3. Observe slots between **3:00 PM** and **5:30 PM**

**Expected Result:** Slots between 3:00 PM and 5:30 PM are unavailable or greyed out. Slots before 3:00 PM and after 5:30 PM are shown as available.

---

### TC-TEE-003 – Book a tee time (solo, Shareholder)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. An available future slot exists.  
**Steps:**
1. Navigate to `/teetimes/book`
2. Select an available future date and time slot
3. Leave additional participants empty (solo booking)
4. Click **Book / Confirm**

**Expected Result:** Booking confirmed. Reservation appears in `/teetimes/my` with the correct date and time.

---

### TC-TEE-004 – Book a tee time with additional participants (foursome)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. An available slot exists.  
**Steps:**
1. Navigate to `/teetimes/book`
2. Select an available future slot
3. Add 3 additional participants using the member IDs of Bob Shareholder, Carol Shareholder, and Diana Silver
4. Click **Book**

**Expected Result:** Booking confirmed for 4 players. That slot is now shown as full (4/4 participants).

---

### TC-TEE-005 – Attempt to add a 5th participant (over limit)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`.  
**Steps:**
1. Navigate to `/teetimes/book`
2. Select a slot
3. Attempt to add 4 additional participants (which would make 5 total)

**Expected Result:** Validation error "Maximum 4 players per tee time" (or equivalent). The 5th participant cannot be added.

---

### TC-TEE-006 – Attempt to double-book the same slot
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. Has an existing booking from TC-TEE-003.  
**Steps:**
1. Navigate to `/teetimes/book`
2. Select the **same** slot that is already booked
3. Click **Book**

**Expected Result:** Error "You already have a booking for this time slot" (or equivalent). Second booking is not created.

---

### TC-TEE-007 – Silver member cannot book a restricted time slot
**Preconditions:** Logged in as `silver@clubbaist.com`. Silver level restricted to before 3 PM / after 5:30 PM (see TC-TEE-002 setup).

> **Setup required:** Same availability configuration from TC-TEE-002 must be in place.

**Steps:**
1. Navigate to `/teetimes/book`
2. Select a slot between **3:00 PM** and **5:30 PM**
3. Click **Book**

**Expected Result:** Error "Your membership level does not permit booking during this time" (or equivalent). Booking is not created.

---

### TC-TEE-008 – Cancel a reservation
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. Has an upcoming reservation (from TC-TEE-003).  
**Steps:**
1. Navigate to `/teetimes/my`
2. Find the upcoming booking and click **Cancel**
3. Confirm the cancellation

**Expected Result:** Reservation is removed from the list. The slot reappears as available for other members.

---

### TC-TEE-009 – Staff Console: Admin creates a reservation for a member
**Preconditions:** Logged in as `admin@clubbaist.com`. A season with available slots exists.  
**Steps:**
1. Navigate to `/teetimes/staff`
2. Select a date and an available time slot
3. Assign the booking to `silver@clubbaist.com` (Diana Silver)
4. Save

**Expected Result:** Booking appears in the staff console for that slot. Logging in as `silver@clubbaist.com` and checking `/teetimes/my` shows the new booking.

---

### TC-TEE-010 – Standing tee time request (Shareholder only)

**Preconditions:** Logged in as `shareholder1@clubbaist.com`. A season exists.  
**Steps (when UI is available):**
1. Navigate to the Standing Tee Time request page
2. Select Day: **Saturday**, Time: **8:00 AM**
3. Add 3 additional participants: Bob Shareholder, Carol Shareholder, Diana Silver (foursome required)
4. Set a date range within the active season and submit

**Future Expected Result:** Request created with status **Draft**. Appears in Alice's standing tee time list.

---

### TC-TEE-011 – Bronze member cannot book outside restricted hours
**Preconditions:** Logged in as `bronze@clubbaist.com` (Evan Bronze).

> **Setup required:** Log in as `admin@clubbaist.com` and restrict the **Bronze** level to:
> before 3:00 PM and after 6:00 PM (remove the 3:00 PM – 6:00 PM window). Then log back in
> as `bronze@clubbaist.com`.

**Steps:**
1. Navigate to `/teetimes/book`
2. Select a slot between **3:00 PM** and **6:00 PM**
3. Click **Book**

**Expected Result:** Error about membership level restriction. Booking is not created.

---

## TC-SCORE: Scorekeeping

> **Setup note for all TC-SCORE cases:** Score entry requires a tee time booking that is in the
> past **and** has passed the minimum round-completion time-lock (solo = 2 h, twosome = 2 h 30 m,
> threesome = 3 h, foursome = 3 h 30 m). The seeder creates a 2026 season with slots from
> January 1 onward, so past slots exist. Before running TC-SCORE tests, log in as
> `admin@clubbaist.com`, navigate to `/teetimes/staff`, and create at least one booking for
> `shareholder1@clubbaist.com` (Alice) on any past date (e.g., April 17, 2026 at 8:00 AM) to use
> as the eligible booking throughout this section.

---

### TC-SCORE-001 – Member views eligible bookings list
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. A past booking with an elapsed time-lock exists (see setup note above).  
**Steps:**
1. Navigate to `/scores/my`

**Expected Result:** The **Eligible rounds available to score** section lists the past booking with the correct date, time, and player count. The **Past Submitted Rounds** section is empty (no rounds yet submitted).

---

### TC-SCORE-002 – Member submits a valid 18-hole round (happy path)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. An eligible booking appears in `/scores/my`.  
**Steps:**
1. Navigate to `/scores/my`
2. Click the eligible booking row (or the **Enter Scores →** button)
3. On the score entry form, select tee colour **White**
4. Enter a score of **5** for every hole (18 holes)
5. Verify the running totals show: Front 9 = 45, Back 9 = 45, Total = 90
6. Click **Submit**

**Expected Result:** Redirected to the submission confirmation page. Confirmation displays the booking date, tee colour (White), and total score (90). No error is shown.

---

### TC-SCORE-003 – Submitted round appears in past rounds history
**Preconditions:** TC-SCORE-002 completed.  
**Steps:**
1. Navigate to `/scores/my`

**Expected Result:** The **Past Submitted Rounds** section lists the just-submitted round showing date, tee colour (White), total score (90), and submitted-on timestamp. The booking no longer appears in the **Eligible rounds** section.

---

### TC-SCORE-004 – No eligible bookings message
**Preconditions:** Logged in as `silver@clubbaist.com` (Diana Silver). No past bookings have been created for this account.  
**Steps:**
1. Navigate to `/scores/my`

**Expected Result:** The **Eligible rounds available to score** section shows a "No eligible rounds" (or equivalent) message instead of a table. No error is thrown.

---

### TC-SCORE-005 – Score entry: score out of range (above maximum)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. A second eligible past booking exists (create one via admin staff console if needed).  
**Steps:**
1. Navigate to `/scores/my` and open the eligible booking
2. Enter a score of **5** for holes 1–17
3. Enter **21** for hole 18
4. Click **Submit**

**Expected Result:** Validation error indicating hole 18 score is out of range (valid range 1–20). The round is **not** submitted; the scorecard form remains with the error highlighted.

---

### TC-SCORE-006 – Score entry: missing hole score
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. An eligible booking exists.  
**Steps:**
1. Navigate to `/scores/my` and open the eligible booking
2. Enter a score of **5** for holes 1–17
3. Leave hole 18 blank
4. Click **Submit**

**Expected Result:** Validation error indicating the scorecard is incomplete (all 18 scores are required). The round is **not** submitted.

---

### TC-SCORE-007 – Duplicate submission (same booking scored twice)
**Preconditions:** TC-SCORE-002 completed — the booking has an existing `GolfRound`.  
**Steps:**
1. Navigate directly to `/scores/record?bookingId=<id>` using the booking ID from TC-SCORE-002
2. Enter valid scores for all 18 holes
3. Click **Submit**

**Expected Result:** Error "Score already submitted for this booking" (or equivalent). A second `GolfRound` record is **not** created.

---

### TC-SCORE-008 – Booking not yet past time-lock (too recent to score)
**Preconditions:** Logged in as `shareholder1@clubbaist.com`. A booking exists whose tee time start was fewer than 2 hours ago (e.g., create a booking for today at a time that is currently less than 2 hours in the past).  
**Steps:**
1. Navigate to `/scores/my`

**Expected Result:** The recent booking does **not** appear in the eligible rounds list (time-lock has not elapsed). No error — it simply isn't offered for scoring yet.

---

### TC-SCORE-009 – Staff scores on behalf of a member (score console)
**Preconditions:** Logged in as `admin@clubbaist.com`. A past booking for `silver@clubbaist.com` (Diana Silver) exists for today's date (create via `/teetimes/staff` if needed) and the time-lock has elapsed.  
**Steps:**
1. Navigate to `/scores/staff`
2. Locate Diana Silver's completed booking in today's grid
3. Click her name / booking row to open the score entry form
4. Select tee colour **Red**
5. Enter a score of **6** for all 18 holes (total = 108)
6. Click **Submit**

**Expected Result:** Submission confirmation shown (date, tee colour Red, total 108). Log in as `silver@clubbaist.com`, navigate to `/scores/my` — the submitted round appears in the **Past Submitted Rounds** history.

---

### TC-SCORE-010 – Role-based access: member cannot access staff score console
**Preconditions:** Logged in as `shareholder1@clubbaist.com`.  
**Steps:**
1. Navigate directly to `/scores/staff`

**Expected Result:** Access denied or redirect to home. Staff score console content is not displayed.

---

## TC-ADMIN: Administration

### TC-ADMIN-001 – Admin views user list
**Preconditions:** Logged in as `admin@clubbaist.com`.  
**Steps:**
1. Navigate to `/admin/users`

**Expected Result:** All seeded users listed. Each entry shows name, email, role, and membership level.

---

### TC-ADMIN-002 – Admin changes a member's membership level
**Preconditions:** Logged in as `admin@clubbaist.com`.  
**Steps:**
1. Navigate to `/admin/users`
2. Select **Diana Silver** (`silver@clubbaist.com`)
3. Navigate to the edit member page (accessible from `/admin/users/{UserId}` → Edit Member link, which opens `/admin/members/{MemberId}`)
4. Change membership level from **Silver** to **Bronze**
5. Save

**Expected Result:** Diana Silver's membership level updated to **Bronze**. Change reflected in the user list and her tee time booking restrictions now match Bronze rules.

---

### TC-ADMIN-003 – Create a new golf season
**Preconditions:** Logged in as `admin@clubbaist.com`.  
**Steps:**
1. Navigate to `/admin/seasons`
2. Click **Create Season**
3. Enter:
   - Name: `2026 Season`
   - Start Date: `2026-05-01`
   - End Date: `2026-10-31`
4. Click **Create** / **Generate Slots**

**Expected Result:** Season created. Tee time slots generated for every day from 2026-05-01 to 2026-10-31 (7 AM – 7 PM at ~8-minute intervals). Slots visible in availability view.

---

### TC-ADMIN-004 – Admin has access to all admin pages
**Preconditions:** Logged in as `admin@clubbaist.com`.  
**Steps:**
1. Navigate to `/admin/users`
2. Navigate to `/admin/seasons`
3. Navigate to `/teetimes/staff`

**Expected Result:** All three pages load successfully without access-denied errors or redirects.

---

## TC-NEG: Negative / Edge Cases

| ID | Scenario | Test Data | Expected Outcome |
|---|---|---|---|
| TC-NEG-001 | Login with non-existent email | Email: `nobody@nowhere.com` / any password | "Invalid login attempt." error shown |
| TC-NEG-002 | Application with blank required field | Leave Occupation empty, submit | Field validation error; form not submitted |
| TC-NEG-003 | Application using email of an existing member | Email: `shareholder1@clubbaist.com` | Error "Email already registered as a member" |
| TC-NEG-004 | Book a tee time slot in the past | Select yesterday's date | Error "Cannot book a slot in the past" |
| TC-NEG-005 | Non-admin navigates to season management | Log in as `silver@clubbaist.com`, go to `/admin/seasons` | Access denied / redirect |
| TC-NEG-006 | Re-approve an already Accepted application | Frank Pending after TC-MEM-002 | Approve action not available (terminal state) |
| TC-NEG-007 | Re-deny an already Denied application | Iris Submitted after TC-MEM-008 | Deny action not available (terminal state) |
| TC-NEG-008 | Submit score with a 0 on any hole | Enter 0 for hole 1, 5 for holes 2–18 | Validation error: score out of range (1–20); round not submitted |
| TC-NEG-009 | Member accesses staff score console | Log in as `silver@clubbaist.com`, go to `/scores/staff` | Access denied / redirect |

---

## End-to-End Smoke Test

Run these cases in order to verify the complete member lifecycle from application to booking:

| Step | Test Case | Account | Action |
|---|---|---|---|
| 1 | TC-AUTH-001 | admin@clubbaist.com | Log in as admin |
| 2 | TC-ADMIN-003 | admin@clubbaist.com | Create / verify 2026 season with tee slots |
| 3 | — | — | Log out; log in as shareholder3@clubbaist.com |
| 4 | TC-MEM-003 | shareholder3@clubbaist.com | Submit Sam Tester's membership application |
| 5 | — | — | Log out; log in as committee@clubbaist.com |
| 6 | TC-MEM-001 | committee@clubbaist.com | Verify Sam Tester appears in inbox (Submitted) |
| 7 | TC-MEM-002 | committee@clubbaist.com | Approve Sam Tester's application |
| 8 | TC-AUTH-001 | sam.tester@example.com / ChangeMe123! | Log in as the newly approved member |
| 9 | TC-TEE-003 | sam.tester@example.com | Book a solo tee time |
| 10 | TC-TEE-008 | sam.tester@example.com | Cancel that reservation |
| 11 | — | — | Log out; log in as admin@clubbaist.com |
| 12 | TC-TEE-009 | admin@clubbaist.com | Create a reservation for Sam via staff console on a past date (e.g., April 17, 2026 8:00 AM) |
| 13 | — | — | Log out; log in as sam.tester@example.com |
| 14 | TC-SCORE-001 | sam.tester@example.com | Verify past booking appears in eligible rounds at `/scores/my` |
| 15 | TC-SCORE-002 | sam.tester@example.com | Submit 18-hole round (all 5s, White tee) — confirm total 90 on confirmation page |
| 16 | TC-SCORE-003 | sam.tester@example.com | Verify submitted round appears in past rounds history; eligible list is empty |

**Pass criteria:** All 16 steps complete without errors or unexpected redirects.
