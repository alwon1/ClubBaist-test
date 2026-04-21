# ClubBaist Manual Test Results

**Date:** 2026-04-20  
**App URL:** `https://localhost:7021`  
**Aspire Dashboard:** `https://localhost:17289`  
**Branch:** main  
**Tester:** GitHub Copilot (automated via Playwright MCP)

---

## Automation Reference Notes

### Helpers used in every test
```
BASE_URL = https://localhost:7021

// Login helper
navigate(BASE_URL + '/Account/Login')
fill(getByRole('textbox', { name: 'Email' }), email)
fill(getByRole('textbox', { name: 'Password' }), password)
click(getByRole('button', { name: 'Log in', exact: true }))

// Logout helper
click(getByRole('button', { name: 'Logout' }))
// → redirected to /Account/Login
```

---

### Parallelization via `mcp_playwright_browser_run_code`

**Discovery:** `mcp_playwright_browser_run_code` exposes a `page` object. Via `page.context().browser()` you get the full Playwright `Browser` instance and can create multiple isolated `BrowserContext` instances — each with their own cookies/session. This enables true parallel testing across different users within a single `run_code` call.

**Alternative — `runSubagent` parallelization:** Each subagent gets its own MCP tool invocations and can drive the browser independently using the step-by-step tools (`mcp_playwright_browser_navigate`, `mcp_playwright_browser_click`, etc.). For large test suites, spawn multiple subagents in parallel — one per user role or test group — and have each write its results to TEST_RESULTS.md when done. Combine with `run_code`-based parallelism for maximum throughput: use subagents to split test groups, and `run_code` + multiple contexts within each subagent to parallelize individual tests.

**Verified available globals:** `page` ✅ — `browser` ❌ — `context` ❌ — `playwright` ❌ — `require` ❌  
Access the browser via: `page.context().browser()`

**Parallel test template:**
```js
async () => {
  const browser = page.context().browser();
  const BASE = 'https://localhost:7021';

  // Helper: login a page object as a given user
  async function login(pg, email, password) {
    await pg.goto(BASE + '/Account/Login');
    await pg.getByRole('textbox', { name: 'Email' }).fill(email);
    await pg.getByRole('textbox', { name: 'Password' }).fill(password);
    await pg.getByRole('button', { name: 'Log in', exact: true }).click();
    await pg.waitForURL(BASE + '/');
  }

  // Create N isolated contexts (each has its own cookies — fully independent)
  const [ctxA, ctxB, ctxC] = await Promise.all([
    browser.newContext({ ignoreHTTPSErrors: true }),
    browser.newContext({ ignoreHTTPSErrors: true }),
    browser.newContext({ ignoreHTTPSErrors: true }),
  ]);
  const [pgA, pgB, pgC] = await Promise.all([
    ctxA.newPage(), ctxB.newPage(), ctxC.newPage(),
  ]);

  // Login all users simultaneously
  await Promise.all([
    login(pgA, 'admin@clubbaist.com', 'Pass@word1'),
    login(pgB, 'shareholder1@clubbaist.com', 'Pass@word1'),
    login(pgC, 'silver@clubbaist.com', 'Pass@word1'),
  ]);

  // Run tests in parallel
  const [resultA, resultB, resultC] = await Promise.all([
    (async () => {
      // ... test steps for admin context ...
      await pgA.screenshot({ path: 'screenshots/TC-XXX-admin.png', type: 'png' });
      return { pass: true, note: '...' };
    })(),
    (async () => {
      // ... test steps for shareholder context ...
      return { pass: true, note: '...' };
    })(),
    (async () => {
      // ... test steps for silver context ...
      return { pass: true, note: '...' };
    })(),
  ]);

  // Always close contexts when done
  await Promise.all([ctxA.close(), ctxB.close(), ctxC.close()]);

  return { resultA, resultB, resultC };
}
```

**Key notes for automated test authoring:**
- Always pass `{ ignoreHTTPSErrors: true }` to `newContext()` — the dev app uses a self-signed cert
- Wait for Blazor components with `waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })`  
- Use `pg.locator('main').innerText()` to read page content; wrap in `.catch(() => '')` for safety
- Screenshots: `pg.screenshot({ path: 'screenshots/TC-XXX.png', type: 'png' })`
- Group tests by which user they need — login once per context, then run all that user's assertions back-to-back
- Tests with sequential dependencies (e.g. TC-SCORE-002 must run before TC-SCORE-003) must stay in the same context in series; only truly independent tests should be parallelized
- **Each subagent / test run is responsible for updating TEST_RESULTS.md itself** — after completing its assigned tests, use `replace_string_in_file` to swap the `⏳ PENDING` stub with the real result, screenshot path, notes, and finalized automation steps. Do not rely on a parent agent to write results later.

**Admin past-date booking override:**  
Admin can create bookings on past dates (needed for score test setup) via direct URL navigation — bypasses the staff console grid which blocks past dates:
```js
await pg.goto(BASE + '/teetimes/book?date=2026-04-17&time=08:00');
await pg.waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 });
// Admin sees a "Booking Member (Admin Override)" dropdown — not present for regular members
await pg.getByLabel('Booking Member (Admin Override)').selectOption('SH-0001 - Alice Shareholder');
await pg.getByRole('button', { name: 'Book Tee Time' }).click();
```

---

## TC-AUTH: Authentication

### TC-AUTH-001 – Successful login
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-AUTH-001-pass-admin-logged-in.png`
- **Notes:** Redirected to `/` (home). Nav shows `admin@clubbaist.com` (email shown, not display name "Seed Admin" — minor discrepancy from test plan expectation of "Seed Admin").

**Automation steps:**
```
navigate(BASE_URL + '/Account/Login')
wait: page title = "Log in"
fill(getByRole('textbox', { name: 'Email' }), 'admin@clubbaist.com')
fill(getByRole('textbox', { name: 'Password' }), 'Pass@word1')
click(getByRole('button', { name: 'Log in', exact: true }))
wait: url = BASE_URL + '/'
assert: getByRole('link', { name: 'admin@clubbaist.com' }).isVisible()
assert: page title = "Club BAIST - Home"
```

---

### TC-AUTH-002 – Failed login (wrong password)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-AUTH-002-pass-invalid-login.png`
- **Notes:** Alert `"Error: Invalid login attempt."` shown. Remained on `/Account/Login`.

**Automation steps:**
```
// Precondition: not logged in
navigate(BASE_URL + '/Account/Login')
wait: page title = "Log in"
fill(getByRole('textbox', { name: 'Email' }), 'admin@clubbaist.com')
fill(getByRole('textbox', { name: 'Password' }), 'wrongpassword')
click(getByRole('button', { name: 'Log in', exact: true }))
wait: getByRole('alert') visible
assert: url contains '/Account/Login'
assert: getByRole('alert').textContent contains 'Invalid login attempt'
```

---

### TC-AUTH-003 – Register a new account
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-AUTH-003-pass-register-success.png`
- **Notes:** Registration succeeded, redirected to home as logged-in user. Confirmation/email-verification step skipped (dev mode). Nav shows `newuser.test@example.com`.

**Automation steps:**
```
navigate(BASE_URL + '/Account/Register')
wait: page title = "Register"
fill(getByRole('textbox', { name: 'Email' }), 'newuser.test@example.com')
fill(getByRole('textbox', { name: 'Password' }), 'Pass@word1')
fill(getByRole('textbox', { name: 'Confirm Password' }), 'Pass@word1')
click(getByRole('button', { name: 'Register' }))
assert: no validation errors visible  OR  url changed (login or home)
```

---

### TC-AUTH-004 – Register with mismatched passwords
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-AUTH-004-pass-password-mismatch.png`
- **Notes:** Validation error: "The password and confirmation password do not match." Form stayed on `/Account/Register`.

**Automation steps:**
```
navigate(BASE_URL + '/Account/Register')
fill(getByRole('textbox', { name: 'Email' }), 'newuser2@example.com')
fill(getByRole('textbox', { name: 'Password' }), 'Pass@word1')
fill(getByRole('textbox', { name: 'Confirm Password' }), 'DifferentPass1')
click(getByRole('button', { name: 'Register' }))
assert: url still contains '/Account/Register'
assert: validation error visible containing 'do not match' or 'passwords'
```

---

### TC-AUTH-005 – Committee cannot access Admin pages
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-AUTH-005-pass-committee-access-denied.png`
- **Notes:** Navigating to `/admin/users` as committee redirected to `/Account/AccessDenied?ReturnUrl=%2Fadmin%2Fusers`. Access denied page rendered.

**Automation steps:**
```
// Login as committee
navigate(BASE_URL + '/Account/Login')
fill email: committee@clubbaist.com / Pass@word1
click Log in
wait: url = BASE_URL + '/'
navigate(BASE_URL + '/admin/users')
assert: url does NOT contain '/admin/users'  OR  access-denied content visible
```

---

### TC-AUTH-006 – Member cannot access Committee inbox
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-AUTH-006-pass-member-access-denied-applications.png`
- **Notes:** Navigating to `/membership/applications` as shareholder1 redirected to `/Account/AccessDenied`. Access denied page rendered.

**Automation steps:**
```
// Login as shareholder1
navigate(BASE_URL + '/Account/Login')
fill email: shareholder1@clubbaist.com / Pass@word1
click Log in
wait: url = BASE_URL + '/'
navigate(BASE_URL + '/membership/applications')
assert: url does NOT contain '/membership/applications'  OR  access-denied content visible
```

---

## TC-MEM: Membership Applications

### TC-MEM-001 – View pre-existing applications (Committee)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-001-pass-applications-inbox.png`
- **Notes:** All 5 pre-seeded applications visible. Default filter shows "All Actionable" (Submitted + OnHold + Waitlisted). Frank Pending, Iris Submitted, Grace OnHold, Henry Waitlist, Jack Waitlist all visible with correct statuses.

**Automation steps:**
```
// Login as committee@clubbaist.com
navigate(BASE_URL + '/membership/applications')
wait: page loaded
assert: row containing 'Frank Pending' visible with status 'Submitted'
assert: row containing 'Iris Submitted' visible with status 'Submitted'
assert: row containing 'Grace OnHold' visible with status 'OnHold'
// Filter for Waitlisted — combobox getByLabel('Filter by Status')
select(getByLabel('Filter by Status'), 'Waitlisted')
assert: 'Henry Waitlist' and 'Jack Waitlist' visible
```

---

### TC-MEM-002 – Review and approve Frank Pending
- **Result: ✅ PASS**
- **Screenshots:** `screenshots/TC-MEM-002-frank-application-detail.png`, `screenshots/TC-MEM-002-pass-frank-accepted.png`, `screenshots/TC-MEM-002b-pass-frank-logged-in.png`
- **Notes:** Approval required two steps: (1) select `Accepted` from New Status combobox → a second "Membership Level" combobox appeared; (2) select membership level; (3) click Submit Decision. Success message: "Application approved. Member account created successfully.". Frank's member number assigned as `AS-0006` (Associate). Frank login with `ChangeMe123!` succeeded. **Default password for approved members is `ChangeMe123!`.**

**Automation steps:**
```
navigate(BASE_URL + '/membership/applications')
click link for 'Frank Pending' → navigates to /membership/applications/1
wait: page title = 'Review Application'
assert: getByText('Associate').isVisible()  // requested level
// Make Decision panel:
select(getByLabel('New Status'), 'Accepted')
// IMPORTANT: a second dropdown appears after selecting Accepted
wait: getByLabel('Membership Level').isVisible()
select(getByLabel('Membership Level'), 'Associate')
click(getByRole('button', { name: 'Submit Decision' }))
wait: alert visible
assert: alert text contains 'Application approved. Member account created successfully.'
assert: getByText('Accepted').isVisible()  // status badge
assert: getByText('No further status transitions are available').isVisible()
// Verify login:
navigate(BASE_URL + '/Account/Login')
fill email: frank.pending@example.com / ChangeMe123!
click(getByRole('button', { name: 'Log in', exact: true }))
assert: url = BASE_URL + '/'
```

---

### TC-MEM-003 – Submit a new membership application
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-003-pass-application-submitted.png`
- **Notes:** Submitted as shareholder3. Sponsor IDs: Alice = member DB ID 1 (SH-0001), Bob = member DB ID 2 (SH-0002). Sponsor fields are `spinbutton` inputs (numeric). Success alert: "Your membership application has been submitted successfully. You will be notified once your application has been reviewed." Form cleared after submission; page stays on `/membership/apply`.

**Automation steps:**
```
// Login as shareholder3@clubbaist.com / Pass@word1
navigate(BASE_URL + '/membership/apply')
fill(getByRole('textbox', { name: 'Email Address' }), 'sam.tester@example.com')
fill(getByRole('textbox', { name: 'First Name' }), 'Sam')
fill(getByRole('textbox', { name: 'Last Name' }), 'Tester')
fill(getByRole('textbox', { name: 'Date of Birth' }), '1990-05-15')
fill(getByRole('textbox', { name: 'Phone (e.g. (403) 555-1234)' }), '(403) 555-1234')
fill(getByRole('textbox', { name: 'Address' }), '100 Test Street')
fill(getByRole('textbox', { name: 'Postal Code (e.g. T2A 4K3)' }), 'T2A 4K3')
fill(getByRole('textbox', { name: 'Occupation' }), 'Software Developer')
fill(getByRole('textbox', { name: 'Company Name' }), 'Test Corp')
select(getByLabel('Membership Level'), 'Bronze')
fill(getByRole('spinbutton', { name: 'Sponsor 1 Member ID' }), '1')  // Alice DB ID
fill(getByRole('spinbutton', { name: 'Sponsor 2 Member ID' }), '2')  // Bob DB ID
click(getByRole('button', { name: 'Submit Application' }))
assert: alert text contains 'submitted successfully'
```

---

### TC-MEM-004 – Duplicate application (same email)
- **Result: ❌ FAIL**
- **Screenshot:** `screenshots/TC-MEM-004-fail-duplicate-allowed.png`
- **Notes:** Submitted the same email (`sam.tester@example.com`) a second time — received the same success message instead of an error. Duplicate applications are accepted without server-side uniqueness validation. **Bug: no duplicate application guard implemented.**

**Automation steps (when fixed):**
```
// Precondition: TC-MEM-003 completed
navigate(BASE_URL + '/membership/apply')
fill all required fields with sam.tester@example.com
click(getByRole('button', { name: 'Submit Application' }))
assert: error message visible containing 'already exists' or 'active application'
// Currently (bug): success message shown instead — test will fail
```

---

### TC-MEM-005 – Invalid postal code
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-005-pass-invalid-postal.png`
- **Notes:** Error shown at top of form AND inline: "Enter a valid Canadian postal code (e.g. T2A 4K3)." Form not submitted; page stayed on `/membership/apply`.

**Automation steps:**
```
navigate(BASE_URL + '/membership/apply')
fill all required fields with valid data EXCEPT:
  fill(getByRole('textbox', { name: 'Postal Code (e.g. T2A 4K3)' }), '12345')
click(getByRole('button', { name: 'Submit Application' }))
assert: url still contains '/membership/apply'
assert: getByRole('listitem').textContent contains 'valid Canadian postal code'
assert: inline error next to Postal Code field visible
```

---

### TC-MEM-006 – Invalid phone number
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-006-pass-invalid-phone.png`
- **Notes:** Error shown at top and inline: "Enter a valid 10-digit Canadian phone number (e.g. (403) 555-1234)." Form not submitted.

**Automation steps:**
```
navigate(BASE_URL + '/membership/apply')
fill all required fields EXCEPT:
  fill(getByRole('textbox', { name: 'Phone (e.g. (403) 555-1234)' }), '123')
click(getByRole('button', { name: 'Submit Application' }))
assert: url still contains '/membership/apply'
assert: error text contains '10-digit Canadian phone number'
```

---

### TC-MEM-007 – Same sponsor twice
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-007-pass-same-sponsor.png`
- **Notes:** Alert shown: "Sponsor 1 and Sponsor 2 must be different members." Form not submitted. Note: this is a server-side (not client-side) validation — the alert appears after submit click.

**Automation steps:**
```
navigate(BASE_URL + '/membership/apply')
fill all required fields
fill(getByRole('spinbutton', { name: 'Sponsor 1 Member ID' }), '1')
fill(getByRole('spinbutton', { name: 'Sponsor 2 Member ID' }), '1')  // same
click(getByRole('button', { name: 'Submit Application' }))
assert: getByRole('alert').textContent contains 'must be different members'
assert: url still contains '/membership/apply'
```

---

### TC-MEM-008 – Deny an application (Iris Submitted)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-008-pass-iris-denied.png`
- **Notes:** Selected 'Denied' from New Status combobox, clicked Submit Decision. Success alert: "Application denied." Status badge changed to Denied. Make Decision panel replaced by terminal state message: "No further status transitions are available for this application."

**Automation steps:**
```
navigate(BASE_URL + '/membership/applications/4')  // Iris = ID 4
wait: page title = 'Review Application'
select(getByLabel('New Status'), 'Denied')
click(getByRole('button', { name: 'Submit Decision' }))
wait: getByRole('alert').isVisible()
assert: alert text = 'Application denied.'
assert: getByText('No further status transitions').isVisible()
assert: getByRole('button', { name: 'Submit Decision' }).not.exists()
```

---

### TC-MEM-009 – Change status to OnHold
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-MEM-009-pass-henry-onhold.png`
- **Notes:** Used Henry Waitlist (ID=3, status was Waitlisted). Selected 'OnHold' from New Status combobox, clicked Submit Decision. Alert: "Application status changed to OnHold." Status badge updated. New Status options after change: Submitted, Waitlisted, Accepted, Denied (OnHold is no longer available as next status — state machine correctly constrains transitions).

**Automation steps:**
```
navigate(BASE_URL + '/membership/applications/3')  // Henry = ID 3
wait: page title = 'Review Application'
select(getByLabel('New Status'), 'OnHold')
click(getByRole('button', { name: 'Submit Decision' }))
wait: getByRole('alert').isVisible()
assert: alert text = 'Application status changed to OnHold.'
assert: getByText('OnHold').isVisible()  // status badge
```

---

### TC-MEM-010 – Accepted application terminal state
- **Result: ✅ PASS**
- **Notes:** After TC-MEM-002, Frank's application at `/membership/applications/1` showed "No further status transitions are available for this application." Make Decision panel replaced entirely — no New Status combobox, no Submit Decision button. Same confirmed for Iris (Denied, ID=4) after TC-MEM-008.

**Automation steps:**
```
navigate(BASE_URL + '/membership/applications/1')  // Frank = ID 1, Accepted
wait: page title = 'Review Application'
assert: getByText('No further status transitions are available').isVisible()
assert: getByLabel('New Status').not.exists()
assert: getByRole('button', { name: 'Submit Decision' }).not.exists()
```

---

## TC-TEE: Tee Time Reservations

### TC-TEE-001 – View availability (Shareholder – unrestricted)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-TEE-001-pass-availability-shareholder.png`
- **Notes:** Date 2026-05-09 selected. All slots from 7:00 AM onward shown as bookable "+" cells (blue dashed). Today's date (2026-04-20) showed all slots as "Not available at this time" — **Note: today's date slots appear restricted (possibly past-date logic or day-of-week rule for today).**  Future dates (e.g. May 9, a Saturday) show full availability. Navigation: date picker input ref `getByRole('textbox', { name: 'Select Date' })`, click Refresh button to reload.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes')
fill(getByRole('textbox', { name: 'Select Date' }), '2026-05-09')
click(getByRole('button', { name: 'Refresh' }))
wait: slots loaded
assert: links with '+' and url '/teetimes/book?date=2026-05-09&time=07:00' visible
assert: no 'Not available at this time for your membership' elements present
```

---

### TC-TEE-003 – Book a tee time (solo, Shareholder)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-TEE-003-pass-solo-booking.png`
- **Notes:** Navigated directly to `/teetimes/book?date=2026-05-09&time=07:00` as shareholder1. Booking form pre-filled with date/time. Clicked "Book Tee Time". Success message: **"Tee time booked successfully for Saturday, May 9, 2026 at 7:00 AM."** Booking assigned reservation ID 1. Verified on `/teetimes/my` — row visible.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/book?date=2026-05-09&time=07:00')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
assert: getByRole('heading', { name: /Book Tee Time/i }).isVisible()
click(getByRole('button', { name: 'Book Tee Time' }))
wait: getByRole('alert').isVisible()
assert: alert text contains 'Tee time booked successfully'
assert: alert text contains 'May 9, 2026'
assert: alert text contains '7:00 AM'
navigate(BASE_URL + '/teetimes/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
assert: row containing 'May 9, 2026' and '7:00 AM' visible
```

---

### TC-TEE-004 – Foursome booking
- **Result: ✅ PASS**
- **Notes:** Navigated to `/teetimes/book?date=2026-05-10&time=07:00` as shareholder1. Used May 10 (Sunday) — May 9 already had a booking. Clicked "+ Add Player" 3 times to add Player 2/3/4 comboboxes. Selected Bob (SH-0002 / member DB ID 2), Carol (SH-0003 / member DB ID 3), Diana (BR-0004 / member DB ID 4) via the player comboboxes. Clicked "Book Tee Time". Success: **"Tee time booked successfully for Sunday, May 10, 2026 at 7:00 AM."** Reservation ID 2.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/book?date=2026-05-10&time=07:00')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
click(getByRole('button', { name: '+ Add Player' }))   // adds Player 2 combobox
click(getByRole('button', { name: '+ Add Player' }))   // adds Player 3 combobox
click(getByRole('button', { name: '+ Add Player' }))   // adds Player 4 combobox
wait: three additional player comboboxes visible
select(getByRole('combobox', { name: 'Player 2' }), 'SH-0002')  // Bob
select(getByRole('combobox', { name: 'Player 3' }), 'SH-0003')  // Carol
select(getByRole('combobox', { name: 'Player 4' }), 'BR-0004')  // Diana
click(getByRole('button', { name: 'Book Tee Time' }))
wait: getByRole('alert').isVisible()
assert: alert text contains 'Tee time booked successfully'
assert: alert text contains 'May 10, 2026'
// Verify slot is full: check availability grid shows 4/4 for this slot
navigate(BASE_URL + '/teetimes')
fill(getByRole('textbox', { name: 'Select Date' }), '2026-05-10')
click(getByRole('button', { name: 'Refresh' }))
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
assert: cell for 7:00 AM shows '4' or 'Full' (not '+' bookable link)
```

---

### TC-TEE-005 – 5th participant over limit
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-TEE-005-pass-max-players-button-hidden.png`
- **Notes:** After selecting shareholder1 as booker and clicking "+ Add Player" 3 times (adding Players 2, 3, 4 — total 4 players), the "+ Add Player" button **disappeared from the DOM** entirely. No error message shown — UI prevention via hiding the button. There is no 5th player slot possible through the UI.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/book?date=2026-05-09&time=09:00')  // any available slot
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
click(getByRole('button', { name: '+ Add Player' }))  // → 2 players
click(getByRole('button', { name: '+ Add Player' }))  // → 3 players
click(getByRole('button', { name: '+ Add Player' }))  // → 4 players
// At 4 players, button is hidden:
assert: getByRole('button', { name: '+ Add Player' }).not.isVisible()
// No error message shown — limit enforced by UI hiding
```

---

### TC-TEE-006 – Double-book same slot
- **Result: ✅ PASS**
- **Notes:** With shareholder1's May 9 solo booking (reservation ID 1) still active at this point, navigated to `/teetimes/book?date=2026-05-09&time=07:00` again as shareholder1. Clicked "Book Tee Time". Error message displayed: **"Unable to create booking. The slot may be full or booking rules prevent this booking."** Booking not created.

**Automation steps:**
```
// Precondition: shareholder1 already has a booking for May 9 at 7:00 AM (from TC-TEE-003)
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/book?date=2026-05-09&time=07:00')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
click(getByRole('button', { name: 'Book Tee Time' }))
wait: getByRole('alert').isVisible()
assert: alert text contains 'Unable to create booking'
// Confirm no second booking created:
navigate(BASE_URL + '/teetimes/my')
assert: only ONE row for May 9 (not two)
```

---

### TC-TEE-008 – Cancel a reservation
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-TEE-008-pass-cancelled.png`
- **Notes:** Navigated to `/teetimes/my` as shareholder1. May 9 solo booking (ID 1) visible. Clicked "Cancel" button — an **inline confirmation row appeared** with "Confirm" and "No" buttons (no modal dialog). Clicked "Confirm". Success message: **"Booking cancelled successfully."** Row removed from active bookings. May 10 foursome (ID 2) remained. Cancellation does **not** use a modal — it's an inline confirm/no pattern.

**Automation steps:**
```
// Precondition: shareholder1 has May 9 7:00 AM booking (reservation ID 1) from TC-TEE-003
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
assert: row containing 'May 9, 2026' visible
click(getByRole('button', { name: 'Cancel' }))  // inline cancel button in booking row
// IMPORTANT: inline confirm appears — NOT a modal dialog
wait: getByRole('button', { name: 'Confirm' }).isVisible()
click(getByRole('button', { name: 'Confirm' }))
wait: getByRole('alert').isVisible()
assert: alert text contains 'Booking cancelled successfully'
assert: row for 'May 9, 2026' no longer visible
assert: row for 'May 10, 2026' still visible (foursome not affected)
```

---

### TC-TEE-009 – Admin creates reservation for member via staff console
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-TEE-009-pass.png`
- **Notes:** Admin navigated to `/teetimes/staff`, selected Diana Silver from the Members tab, clicked "Book on Behalf" to reveal the inline availability grid, changed date to 2026-04-22 (Wednesday) and clicked Refresh. Clicked the 9:00 AM slot → navigated to `/teetimes/book?date=2026-04-22&time=09:00&bookFor=4` with "Booking Member (Admin Override)" pre-set to "SV-0004 - Diana Silver". Clicked "Book Tee Time". Success message: **"Tee time booked successfully for Wednesday, April 22, 2026 at 9:00 AM."**

**Automation steps (when unblocked):**
```
// Option A: Staff console — requires a future date slot that exists
// Login as admin@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/staff')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
// Select a member from the dropdown:
select(getByLabel('Select Member'), 'BR-0004 - Diana Silver')  // or current membership level prefix
click(getByRole('button', { name: 'Book on Behalf' }))   // reveals availability grid
// Click a future '+' cell for the desired date/time:
click(getByRole('link', { name: '+' }).first())  // first available slot in grid
wait: getByRole('alert').isVisible()
assert: alert text contains 'booked successfully'
// Verify as Diana Silver:
navigate(BASE_URL + '/Account/Login')
fill email: silver@clubbaist.com / Pass@word1
click(getByRole('button', { name: 'Log in', exact: true }))
navigate(BASE_URL + '/teetimes/my')
assert: booking created by admin visible with correct date/time
```

---

### TC-TEE-010 – Standing tee time request (Shareholder)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-TEE-010-pass.png`
- **Notes:** Logged in as shareholder1 (Alice). Navigated to `/teetimes/standing/request`. Form pre-populated with Saturday, 08:00, ±30 min tolerance, start 2026-04-20, end 2026-10-20. Selected Player 2 = Bob (SH-0002), Player 3 = Carol (SH-0003), Player 4 = Diana (SV-0004). Clicked "Submit Request". Success message: **"Your standing tee time request has been submitted and is pending review."** "View My Standing Requests" link shown (→ `/teetimes/standing/my`).

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/standing/request')
wait: page title = 'Request Standing Tee Time - Club Baist'
select(getByLabel('Day of Week *'), 'Saturday')
fill(getByLabel('Requested Time *'), '08:00')
select(getByLabel('Player 2'), 'SH-0002 - Bob Shareholder')
select(getByLabel('Player 3'), 'SH-0003 - Carol Shareholder')
select(getByLabel('Player 4'), 'SV-0004 - Diana Silver')
click(getByRole('button', { name: 'Submit Request' }))
assert: text 'Your standing tee time request has been submitted and is pending review.' visible
assert: getByRole('link', { name: 'View My Standing Requests' }).isVisible()
```

---

## TC-ADMIN: Administration

### TC-ADMIN-001 – Admin views user list
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-ADMIN-001-pass-user-list.png`
- **Notes:** All 9 users listed (admin, committee, 3 shareholders, silver, bronze, frank.pending, newuser.test). Each row shows email, role badge, member number (or —), and Manage/Edit Member action links. Member DB IDs discovered: Alice (shareholder1) = member ID 1 (SH-0001), Bob (shareholder2) = member ID 2 (SH-0002), Carol (shareholder3) = member ID 3 (SH-0003), Diana Silver = member ID 4 (SV-0004), Evan Bronze = member ID 5 (BR-0005). Frank Pending = member ID 6 (AS-0006) after approval.

**Automation steps:**
```
// Login as admin@clubbaist.com / Pass@word1
navigate(BASE_URL + '/admin/users')
wait: page title = 'User Management - Club Baist'
assert: getByRole('row', { name: /shareholder1@clubbaist.com/ }).isVisible()
assert: getByRole('row', { name: /SH-0001/ }).isVisible()
assert: getByRole('row', { name: /frank.pending@example.com.*AS-0006/ }).isVisible()
// Member DB IDs (needed for booking/score tests):
// Alice=1, Bob=2, Carol=3, Diana=4, Evan=5, Frank=6
```

---

### TC-ADMIN-002 – Change Diana Silver's membership to Bronze
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-ADMIN-002-pass-diana-bronze.png`
- **Notes:** Navigated to `/admin/users`, clicked 'Edit Member' for Diana Silver → `/admin/members/4`. Selected Bronze, clicked Save. Alert: "Member profile updated successfully." Heading changed from "Edit Member SV-0004" to "Edit Member BR-0004" — member number prefix changed from SV to BR.

**Automation steps:**
```
navigate(BASE_URL + '/admin/users')
// Diana Silver is member DB ID 4
click(getByRole('link', { name: 'Edit Member' }).nth(3))  // nth(3) = Diana's row
// OR navigate directly:
navigate(BASE_URL + '/admin/members/4')
wait: page title = 'Edit Member - Club Baist'
select(getByLabel('Membership Level'), 'Bronze')
click(getByRole('button', { name: 'Save Changes' }))
wait: getByText('Member profile updated successfully.').isVisible()
assert: page heading contains 'BR-0004'  // member number prefix changed
```

---

### TC-ADMIN-003 – Create a new golf season
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-ADMIN-003-pass-season-created.png`
- **Notes:** 2026 season was pre-seeded. Tested by creating **2027 Season** (2027-01-01 to 2027-12-31). Alert: "Season '2027 Season' added successfully with tee time slots generated." Add Season button reveals inline form (not a separate page). Fields: Season Name (text), Start Date (date), End Date (date). After submission form dismisses and new row appears in table.

**Automation steps:**
```
navigate(BASE_URL + '/admin/seasons')
wait: page title = 'Season Management - Club Baist'
click(getByRole('button', { name: 'Add Season' }))
wait: getByRole('textbox', { name: 'Season Name' }).isVisible()
fill(getByRole('textbox', { name: 'Season Name' }), '2027 Season')
fill(getByRole('textbox', { name: 'Start Date' }), '2027-01-01')
fill(getByRole('textbox', { name: 'End Date' }), '2027-12-31')
click(getByRole('button', { name: 'Add Season' }))  // submit button inside form
wait: getByText("Season '2027 Season' added successfully").isVisible()
assert: getByRole('row', { name: /2027 Season.*Jan 1, 2027.*Dec 31, 2027/ }).isVisible()
```

---

### TC-ADMIN-004 – Admin accesses all admin pages
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-ADMIN-004-pass-all-admin-pages.png`
- **Notes:** Verified as admin: `/admin/users` (User Management), `/admin/seasons` (Season Management), `/teetimes/staff` (Staff Console), `/scores/staff` (Score Console), `/admin/standing-teetimes` (Standing Tee Times), `/membership/applications` (Applications). All loaded without access-denied errors. Admin nav shows: Tee Times, Staff Console, Score Console, User Management, Seasons, Standing Tee Times, Applications.

**Automation steps:**
```
// Login as admin@clubbaist.com / Pass@word1
for url in ['/admin/users', '/admin/seasons', '/teetimes/staff', '/scores/staff', '/admin/standing-teetimes', '/membership/applications']:
  navigate(BASE_URL + url)
  assert: url does NOT contain '/Account/AccessDenied'
  assert: page title does NOT equal 'Access denied'
```

---

---

## TC-NEW-MEMBER: New Member Permissions Verification

### Verify Frank Pending – Role, Membership Level, and Default Password
- **Result: ✅ PASS**
- **Screenshots:** `screenshots/TC-MEM-002b-pass-frank-logged-in.png`, `screenshots/TC-ADMIN-001-pass-user-list.png`
- **Notes:** After TC-MEM-002 approval:
  - Frank's member number: **AS-0006** (Associate level, member DB ID 6)
  - Default password set by system: **`ChangeMe123!`** — login with this succeeded
  - Role: **Member** (confirmed via nav showing "Tee Times", "My Reservations", "My Scores", "My Standing Requests" — standard member nav)
  - No admin or committee nav items visible — correct role isolation
  - Membership level: **Associate** (AS prefix on member number confirms this)
  - Frank can navigate to `/teetimes` and `/membership/apply` without access denied

**Automation steps:**
```
// After approval (TC-MEM-002), login as new member:
navigate(BASE_URL + '/Account/Login')
fill email: frank.pending@example.com / ChangeMe123!
click(getByRole('button', { name: 'Log in', exact: true }))
assert: url = BASE_URL + '/'
assert: getByRole('link', { name: 'Tee Times' }).isVisible()
assert: getByRole('link', { name: 'My Reservations' }).isVisible()
assert: getByRole('link', { name: 'Applications' }).not.isVisible()  // no committee access
assert: getByRole('link', { name: 'User Management' }).not.isVisible()  // no admin access
// Confirm member number via admin:
navigate(BASE_URL + '/admin/users')  // as admin
assert: row for frank.pending@example.com shows role 'Member' and member# 'AS-0006'
```

---

## TC-SCORE: Scorekeeping

> **Setup note:** Score tests require a past booking with elapsed time-lock. Use admin `/teetimes/staff` to create a booking for Alice (shareholder1, member DB ID=1) on a past date (e.g. 2026-04-17 at 08:00). TC-SCORE-009 requires a separate past booking for Diana (silver@clubbaist.com, member DB ID=4).

### TC-SCORE-001 – Member views eligible bookings list
- **Result: ✅ PASS**
- **Screenshots:** `screenshots/TC-SCORE-001-explore.png`, `screenshots/TC-SCORE-001-pass-eligible-list.png`
- **Notes:** Logged in as shareholder1 (Alice). `/scores/my` showed "Eligible rounds available to score" section with one booking: **Apr 20, 2026 at 3:00 PM, 1 player** with a "Record Score" link (→ `/scores/record?bookingId=1&memberId=1`). "Past Submitted Rounds" section showed "No rounds submitted" (empty). Page loaded with Blazor spinner then content.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
assert: text 'Eligible rounds available to score' visible
assert: row with date 'Apr 20, 2026' visible with 'Record Score' link
assert: link href contains '/scores/record?bookingId=1&memberId=1'
assert: 'Past Submitted Rounds' section shows no rounds
```

---

### TC-SCORE-002 – Member submits a valid 18-hole round (happy path)
- **Result: ✅ PASS**
- **Screenshots:** `screenshots/TC-SCORE-002-before-submit.png`, `screenshots/TC-SCORE-002-pass.png`
- **Notes:** Navigated directly to `/scores/record?bookingId=1&memberId=1`. Selected White tee. Clicked first `input[type="number"]`, typed each value (5) and pressed Tab to move to next — this pattern reliably triggers Blazor `@onchange` for all 18 inputs. After filling all 18, the Submit Round button enabled. Clicked Submit. Confirmation page shown: **"Score Submitted — Round Confirmed"** with Date: Monday April 20 2026, Time: 3:00 PM, Tee Colour: White, Total Score: **90**, Submitted On: Apr 20 2026 6:52 PM.
- **Score inputs:** 18 `input[type="number"]` elements (indexed 0-17), `min="1"`, no IDs — use `locator('input[type="number"]').nth(i)`. Correct fill pattern: click input, `keyboard.type(value)`, `keyboard.press('Tab')`, wait 50ms. Do this for all 18 holes.
- **Submit button:** disabled until ALL 18 inputs have valid values via `AllScoresEntered` Blazor computed property.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/record?bookingId=1&memberId=1')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
click(locator('#tee-White'))  // select White tee colour
// Fill all 18 holes with 5:
click(locator('input[type="number"]').first())  // focus first input
for i in 0..17:
  keyboard.type('5')
  keyboard.press('Tab')
  wait 50ms
click(locator('h3'))  // blur last input (trigger Blazor onchange)
wait 300ms
assert: button:has-text('Submit Round') NOT disabled
click(button:has-text('Submit Round'))
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
assert: page text contains 'Score Submitted'
assert: page text contains 'Total Score'
assert: page text contains '90'
```

---

### TC-SCORE-003 – Submitted round appears in history
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-003-pass-history.png`
- **Notes:** After TC-SCORE-002 submission, navigated to `/scores/my`. "Eligible rounds available to score" showed **"No eligible rounds available to score at this time."** — the scored booking no longer appears. "Past Submitted Rounds" table showed one row: **Date: Apr 20 2026, Tee: White, Total Score: 90, Submitted On: Apr 20 2026 6:52 PM**.

**Automation steps:**
```
// After TC-SCORE-002
navigate(BASE_URL + '/scores/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
assert: text 'No eligible rounds available to score at this time' visible
assert: 'Past Submitted Rounds' table has row with 'White', '90', 'Apr 20, 2026'
assert: NO 'Record Score' links visible
```

---

### TC-SCORE-004 – No eligible bookings message
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-004-pass-no-eligible.png`
- **Notes:** Logged in as silver@clubbaist.com (Diana, BR-0004 — no past bookings in system). Navigated to `/scores/my`. Page showed: **"No eligible rounds available to score at this time."** No error or exception. Page loaded cleanly.

**Automation steps:**
```
// Login as silver@clubbaist.com / Pass@word1 (Diana has no past bookings)
navigate(BASE_URL + '/scores/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
assert: page text contains 'No eligible rounds available to score at this time'
assert: no error/exception content visible
```

---

### TC-SCORE-005 – Score out of range (hole scored as 21)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-005-result.png`
- **Notes:** Navigated to `/scores/record?bookingId=4&memberId=1` (Alice's Apr 19 8AM eligible booking). Selected White tee. Filled holes 1–17 with 5, entered 21 for hole 18. The input DOM showed "21" briefly, but Blazor `OnScoreChanged` nulled the value (> 20 check via `max="20"` + server-side clamp). Submit Round button remained **disabled**. Running total reflected only the valid 17 values. The out-of-range value was silently discarded, preventing submission.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/record?bookingId=4&memberId=1')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
click(locator('#tee-White'))
click(locator('input[type="number"]').first())
for i in 0..16:  // holes 1–17
  keyboard.type('5')
  keyboard.press('Tab')
  wait 50ms
keyboard.type('21')  // hole 18 — out of range
keyboard.press('Tab')
wait 300ms
assert: button:has-text('Submit Round') IS disabled
assert: hole 18 input value is null/empty (Blazor nulled it)
```

---

### TC-SCORE-006 – Missing hole score (incomplete scorecard)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-006-result.png`
- **Notes:** Navigated to `/scores/record?bookingId=1&memberId=1`. Selected White tee. Filled holes 1–17 with 5, left hole 18 blank. Submit Round button remained **disabled** with inline message: **"All 18 hole scores are required before submitting."** Running total showed Front 9 = 45, Back 9 = "—" (incomplete), Total = "—". Could not submit.
- **Validation mechanism:** The Submit button is disabled via Blazor `disabled="@(!AllScoresEntered)"` — `AllScoresEntered` is `scores.All(s => s.HasValue)`. No server round-trip needed — pure client-side gate.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/record?bookingId=1&memberId=1')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
click(locator('#tee-White'))
// Fill holes 1-17 only:
click(locator('input[type="number"]').first())
for i in 0..16:  // 17 holes
  keyboard.type('5')
  keyboard.press('Tab')
  wait 50ms
// Leave hole 18 blank — Tab away from it without typing
keyboard.press('Tab')
wait 300ms
assert: button:has-text('Submit Round') IS disabled
assert: text 'All 18 hole scores are required before submitting' visible
```

---

### TC-SCORE-007 – Duplicate submission (same booking scored twice)
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-007-result.png`
- **Notes:** After TC-SCORE-002 (bookingId=1 scored White 90), navigated directly to `/scores/record?bookingId=1&memberId=1`. Page loaded the score entry form (White tee pre-selected from previous submission data). Filled all 18 holes and clicked "Submit Round". Server returned error: **"Score already submitted for this booking."** No second round created. The booking correctly blocked duplicate scoring.

**Automation steps:**
```
// Precondition: TC-SCORE-002 completed — bookingId=1 has score White, 90
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/record?bookingId=1&memberId=1')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
click(locator('#tee-White'))
// Fill all 18 holes
for i in 0..17:
  keyboard.type('5')
  keyboard.press('Tab')
  wait 50ms
click(button:has-text('Submit Round'))
wait: getByRole('alert').isVisible()
assert: alert text contains 'Score already submitted for this booking'
assert: url still contains '/scores/record'
```

---

### TC-SCORE-008 – Booking not yet past time-lock
- **Result: 🚫 BLOCKED**
- **Notes:** bookingId=2 (Alice + Diana, Apr 20 6:00 PM — today's date, 2 players) was confirmed NOT present in Alice's eligible scoring list on `/scores/my`. The slot was booked at ~6 PM today and the time-lock window had not yet elapsed, confirming the time-lock mechanism works. However, the booking was created by seeder (not during test session) so we cannot precisely control its creation timestamp for a repeatable automated test. The time-lock logic is also covered by unit tests in `ScoreServiceTests`. Marked blocked for manual testing purposes — confirmed working in practice.

**Automation steps (when environment supports time injection):**
```
// Precondition: create a booking with slot start = now - 1 hour (< 2h elapsed for solo)
// Login as shareholder1@clubbaist.com
navigate(BASE_URL + '/scores/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
assert: the recent booking does NOT appear in 'Eligible rounds available to score'
assert: it appears only after 2h have elapsed from tee time start
```

---

### TC-SCORE-009 – Staff scores on behalf of a member
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-009-pass.png`
- **Notes:** Admin navigated directly to `/scores/record?bookingId=5&memberId=4` (Diana's Apr 19 9AM seeded booking). Score Console form loaded with tee selection and 18 hole inputs. Selected Red tee. Entered 6 for all 18 holes (total = 108). Clicked "Submit Round". Score submission confirmed. Logged in as Diana (`silver@clubbaist.com`), navigated to `/scores/my`. "Past Submitted Rounds" table showed: **Date: Apr 19, 2026, Tee: Red, Total Score: 108**.

**Automation steps:**
```
// Login as admin@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/record?bookingId=5&memberId=4')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
click(locator('#tee-Red'))  // select Red tee colour
click(locator('input[type="number"]').first())
for i in 0..17:
  keyboard.type('6')
  keyboard.press('Tab')
  wait 50ms
click(button:has-text('Submit Round'))
wait: page text contains 'Score Submitted' OR 'Round Confirmed'
assert: page text contains '108'
// Verify as Diana:
navigate(BASE_URL + '/Account/Login')
fill email: silver@clubbaist.com / Pass@word1
click(getByRole('button', { name: 'Log in', exact: true }))
navigate(BASE_URL + '/scores/my')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
assert: 'Past Submitted Rounds' row with 'Red', '108', 'Apr 19, 2026' visible
```

---

### TC-SCORE-010 – Member cannot access staff score console
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-SCORE-010-pass-access-denied.png`
- **Notes:** Logged in as shareholder1@clubbaist.com. Navigated to `/scores/staff`. Immediately redirected to `/Account/AccessDenied`. Access denied page rendered. Staff score console content never shown.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/staff')
assert: url contains '/Account/AccessDenied'
assert: page content does NOT contain 'Score Console' or staff-specific headings
```

---

## TC-NEG: Negative / Edge Cases

### TC-NEG-001 – Login with non-existent email
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-NEG-001-pass.png`
- **Notes:** Entered `nobody@nowhere.com` with password `anything`. URL stayed at `/Account/Login`. Alert shown: **"Error: Invalid login attempt."**

**Automation steps:**
```
navigate(BASE_URL + '/Account/Login')
fill: Email = 'nobody@nowhere.com', Password = 'anything'
click Log in
assert: url still contains '/Account/Login'
assert: alert contains 'Invalid login attempt'
```

---

### TC-NEG-002 – Application with blank required field
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-NEG-002-pass.png`
- **Notes:** Filled all required fields on `/membership/apply` except Occupation (left blank). Clicked Submit Application. Page stayed on `/membership/apply`. Validation error displayed: **"Occupation is required."**

**Automation steps:**
```
navigate(BASE_URL + '/membership/apply')
fill all required fields EXCEPT Occupation (leave blank)
click Submit
assert: url still /membership/apply
assert: validation error contains 'Occupation is required'
```

---

### TC-NEG-003 – Application using existing member email
- **Result: ❌ FAIL**
- **Screenshot:** `screenshots/TC-NEG-003-fail.png`
- **Notes:** Submitted a membership application using `shareholder1@clubbaist.com` (Alice's email — an existing member). The form accepted the submission and displayed the standard **success message** instead of a duplicate-email error. **Bug: no server-side uniqueness check on application email against existing member accounts.** Same root cause as TC-MEM-004 (duplicate application submissions also accepted).

**Automation steps (when fixed):**
```
navigate(BASE_URL + '/membership/apply')
fill: Email = 'shareholder1@clubbaist.com' + other required fields
click Submit
assert: error 'Email already registered as a member' or equivalent
// Currently (bug): success message shown instead — test will FAIL
```

---

### TC-NEG-004 – Book tee time slot in the past
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-NEG-004-pass.png`
- **Notes:** As shareholder1, navigated to `/teetimes/book?date=2026-04-19&time=08:00` (yesterday). Booking form loaded but clicking "Book Tee Time" returned error: **"Unable to create booking. The slot may be full or booking rules prevent this booking."** The exact wording differs from expected "Cannot book a slot in the past", but the booking was correctly blocked. PASS.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/teetimes/book?date=2026-04-19&time=08:00')  // yesterday
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 10000 })
click(getByRole('button', { name: 'Book Tee Time' }))
wait: getByRole('alert').isVisible()
assert: alert text contains 'Unable to create booking'
assert: url still contains '/teetimes/book'
```

---

### TC-NEG-005 – Non-admin navigates to season management
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-NEG-005-pass.png`
- **Notes:** Logged in as Diana (`silver@clubbaist.com`, Silver member). Navigated to `/admin/seasons`. Immediately redirected to `/Account/AccessDenied`. Access denied page: **"You do not have access to this resource."**

**Automation steps:**
```
navigate(BASE_URL + '/Account/Login')
login as silver@clubbaist.com / Pass@word1
navigate(BASE_URL + '/admin/seasons')
assert: url contains '/Account/AccessDenied'
assert: page text contains 'You do not have access'
```

---

### TC-NEG-006 – Re-approve already Accepted application
- **Result: ✅ PASS**
- **Notes:** Navigated to `/membership/applications/1` (Frank's application, Accepted after TC-MEM-002). Page showed "No further status transitions are available for this application." The Make Decision panel with the "Submit Decision" button was **absent** — replaced by the terminal-state message. No re-approve action possible.

**Automation steps:**
```
// Frank Pending is Accepted after TC-MEM-002
navigate(BASE_URL + '/membership/applications/1')
wait: page title = 'Review Application'
assert: getByText('No further status transitions are available').isVisible()
assert: getByRole('button', { name: 'Submit Decision' }).not.exists()
```

---

### TC-NEG-007 – Re-deny already Denied application
- **Result: ✅ PASS**
- **Notes:** Navigated to `/membership/applications/4` (Iris's application, Denied after TC-MEM-008). Page showed "No further status transitions are available for this application." The Make Decision panel with "Submit Decision" button was **absent** — terminal state enforced. No re-deny action possible.

**Automation steps:**
```
// Iris Submitted is Denied after TC-MEM-008
navigate(BASE_URL + '/membership/applications/4')
wait: page title = 'Review Application'
assert: getByText('No further status transitions are available').isVisible()
assert: getByRole('button', { name: 'Submit Decision' }).not.exists()
```

---

### TC-NEG-008 – Score out of range (score = 0, below minimum)
- **Result: ✅ PASS**
- **Notes:** On the score entry form (`/scores/record?bookingId=4&memberId=1`), entered 0 for hole 1 and filled holes 2–18 with valid values. Blazor `OnScoreChanged` nulled the 0 (below `min=1`), same as > 20 clamp. Submit Round button stayed **disabled**. Bottom message shown: **"All 18 hole scores are required before submitting."** Score of 0 was silently discarded.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/record?bookingId=4&memberId=1')
waitForFunction(() => !document.body.innerText.includes('Loading...'), { timeout: 20000 })
click(locator('#tee-White'))
click(locator('input[type="number"]').first())
keyboard.type('0')  // hole 1 — out of range (min=1)
keyboard.press('Tab')
for i in 1..17:  // holes 2–18
  keyboard.type('5')
  keyboard.press('Tab')
  wait 50ms
wait 300ms
assert: button:has-text('Submit Round') IS disabled
assert: hole 1 input value is null/empty (Blazor nulled it)
assert: text 'All 18 hole scores are required before submitting' visible
```

---

### TC-NEG-009 – Member cannot access staff score console
- **Result: ✅ PASS**
- **Screenshot:** `screenshots/TC-NEG-009-pass.png`
- **Notes:** Logged in as shareholder1 (`shareholder1@clubbaist.com`, Shareholder). Navigated to `/scores/staff`. Immediately redirected to `/Account/AccessDenied`. Same access-denied behaviour as TC-NEG-005 for season management. Staff Score Console content never displayed.

**Automation steps:**
```
// Login as shareholder1@clubbaist.com / Pass@word1
navigate(BASE_URL + '/scores/staff')
assert: url contains '/Account/AccessDenied'
assert: page content does NOT contain 'Score Console' or staff-specific headings
```

---

## Summary

| Category | Total | Pass | Fail | Blocked/Skip | Pending |
|---|---|---|---|---|---|
| TC-AUTH | 6 | 6 | 0 | 0 | 0 |
| TC-MEM | 10 | 9 | 1 | 0 | 0 |
| TC-TEE | 8 | 8 | 0 | 0 | 0 |
| TC-ADMIN | 4 | 4 | 0 | 0 | 0 |
| TC-NEW-MEMBER | 1 | 1 | 0 | 0 | 0 |
| TC-SCORE | 10 | 9 | 0 | 1 | 0 |
| TC-NEG | 9 | 8 | 1 | 0 | 0 |
| **Total** | **48** | **45** | **2** | **1** | **0** |

**Failures:**
- TC-MEM-004: Duplicate application accepted (no uniqueness guard on application email vs existing members)
- TC-NEG-003: Application with existing member email accepted (same root cause as TC-MEM-004)

**Blocked:**
- TC-SCORE-008: Time-lock window testing — time-lock working in practice (bookingId=2 not in eligible list), but no time-injection mechanism for repeatable automated test

_Last updated: 2026-04-21 — all tests complete_
