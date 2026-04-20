# ClubBaist Manual Test Results
**Date:** 2026-04-20  
**App URL:** https://localhost:56188  
**Branch:** fix  
**Runner:** GitHub Copilot (Playwright MCP)

---

## Legend
- ✅ PASS
- ❌ FAIL
- ⏭️ SKIP
- 🔄 IN PROGRESS

---

## TC-AUTH: Authentication

### TC-AUTH-001 – Successful login
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:56188/Account/Login | — |
| Click | textbox[Email] ref=e30 | ~100 |
| Type | `admin@clubbaist.com` | — |
| Click | textbox[Password] ref=e32 | ~100 |
| Type | `Pass@word1` | — |
| Click | button[Log in] ref=e37 | ~100 |

**Result:** Redirected to `/`, nav shows `admin@clubbaist.com` and Logout button.  
**Notes:** —

---

### TC-AUTH-002 – Failed login (wrong password)
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:56188/Account/Login | — |
| Click | textbox[Email] ref=e30 | ~100 |
| Type | `admin@clubbaist.com` | — |
| Click | textbox[Password] ref=e32 | ~100 |
| Type | `wrongpassword` | — |
| Click | button[Log in] ref=e37 | ~100 |

**Result:** Stayed on `/Account/Login`, alert shows `"Error: Invalid login attempt."`  
**Notes:** —

---

### TC-AUTH-003 – Register a new account
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:56188/Account/Register | — |
| Click | textbox[Email] ref=e29 | ~100 |
| Type | `newuser.test@example.com` | — |
| Click | textbox[Password] ref=e31 | ~100 |
| Type | `Pass@word1` | — |
| Click | textbox[Confirm Password] ref=e33 | ~100 |
| Type | `Pass@word1` | — |
| Click | button[Register] ref=e34 | ~100 |

**Result:** Redirected to `/`, nav shows `newuser.test@example.com` — account created and logged in.  
**Notes:** —

---

### TC-AUTH-004 – Register with mismatched passwords
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:56188/Account/Register | — |
| Click | textbox[Email] ref=e29 | ~100 |
| Type | `newuser2@example.com` | — |
| Click | textbox[Password] ref=e31 | ~100 |
| Type | `Pass@word1` | — |
| Click | textbox[Confirm Password] ref=e33 | ~100 |
| Type | `DifferentPass1` | — |
| Click | button[Register] ref=e34 | ~100 |

**Result:** Stayed on `/Account/Register`, alert: `"The password and confirmation password do not match."`  
**Notes:** —

---

### TC-AUTH-005 – Committee cannot access Admin pages
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Login | committee@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:56188/admin/users | — |

**Result:** Redirected to `/Account/AccessDenied?ReturnUrl=%2Fadmin%2Fusers`, page shows `"Access denied. You do not have access to this resource."`  
**Notes:** —

---

### TC-AUTH-006 – Member cannot access Committee inbox
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Login | shareholder1@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:56188/membership/applications | — |

**Result:** Redirected to `/Account/AccessDenied?ReturnUrl=%2Fmembership%2Fapplications`, page shows `"Access denied. You do not have access to this resource."`  
**Notes:** —

---

## TC-MEM: Membership Applications

### TC-MEM-001 – View pre-existing applications
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Login | committee@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:57539/membership/applications | — |
| Read | table[Application Inbox] ref=e105 | — |

**Result:** Inbox displayed 5 seed applications: Frank Pending (Submitted), Grace OnHold (OnHold), Henry Waitlist (Waitlisted), Iris Submitted (Submitted), Jack Waitlist (Waitlisted). All rows visible under "All Actionable" filter.  
**Notes:** —

---

### TC-MEM-002 – Review and approve an existing application
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/membership/applications | — |
| Click | link[Review] ref=e123 (Frank Pending row) | ~100 |
| Navigate | /membership/applications/1 | — |
| Verify | All fields: Frank Pending, frank.pending@example.com, DOB 1990-03-22, Associate, Sponsors Alice+Bob | — |
| Select | combobox[New Status] ref=e153 → "Accepted" | ~200 |
| Select | combobox[Membership Level] ref=e155 → "Associate" | ~200 |
| Click | button[Submit Decision] ref=e156 | ~100 |

**Result:** Alert: "Application approved. Member account created successfully." Status shows **Accepted**. No further transitions available.  
**Notes:** New URL after app relaunch: https://localhost:57539

---

### TC-MEM-003 – Submit a new membership application
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Logout committee | button[Logout] ref=e32 | ~100 |
| Login | shareholder3@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:57539/membership/apply | — |
| Fill | textbox[Email Address] ref=e39 → `sam.tester@example.com` | — |
| Fill | textbox[First Name] ref=e43 → `Sam` | — |
| Fill | textbox[Last Name] ref=e46 → `Tester` | — |
| Fill | textbox[Date of Birth] ref=e50 → `1988-05-12` | — |
| Fill | textbox[Phone] ref=e54 → `(587) 555-9876` | — |
| Fill | textbox[Alternate Phone] ref=e57 → `(587) 555-0000` | — |
| Fill | textbox[Address] ref=e59 → `456 Birdie Ave, Calgary, AB` | — |
| Fill | textbox[Postal Code] ref=e63 → `T3A 2B5` | — |
| Fill | textbox[Occupation] ref=e71 → `Software Engineer` | — |
| Fill | textbox[Company Name] ref=e74 → `Acme Corp` | — |
| Select | combobox[Membership Level] ref=e80 → `Associate` | ~200 |
| Fill | spinbutton[Sponsor 1 Member ID] ref=e84 → `1` (Alice/SH-0001) | — |
| Fill | spinbutton[Sponsor 2 Member ID] ref=e87 → `2` (Bob/SH-0002) | — |
| Click | button[Submit Application] ref=e89 | ~100 |

**Result:** Alert: "Your membership application has been submitted successfully. You will be notified once your application has been reviewed."  
**Notes:** Application for Sam Tester created as Submitted.

---

### TC-MEM-004 – Submit duplicate application (same email)
**Status:** ❌ FAIL  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/membership/apply | — |
| Fill | textbox[Email Address] ref=e39 → `sam.tester@example.com` | — |
| Fill all required fields | (valid data, same info as MEM-003) | — |
| Click | button[Submit Application] ref=e89 | ~100 |

**Result:** Form submitted successfully — duplicate application created. Expected error "An active application already exists for this email" but got success message.  
**Notes:** ⚠️ Duplicate-application validation is not implemented. Bug: duplicate applications with same email are accepted.

---

### TC-MEM-005 – Submit application with invalid postal code
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/membership/apply | — |
| Fill | textbox[Postal Code] ref=e63 → `12345` | — |
| Fill all other required fields | (valid data) | — |
| Click | button[Submit Application] ref=e89 | ~100 |

**Result:** List validation error ref=e91: "Enter a valid Canadian postal code (e.g. T2A 4K3)." Inline field error ref=e92. Form not submitted.  
**Notes:** —

---

### TC-MEM-006 – Submit application with invalid phone number
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| (Same session, postal fixed to T2A 4K3) | — | — |
| Fill | textbox[Phone] ref=e54 → `123` | — |
| Click | button[Submit Application] ref=e89 | ~100 |

**Result:** List validation error ref=e94: "Enter a valid 10-digit Canadian phone number (e.g. (403) 555-1234)." Inline field error ref=e95. Form not submitted.  
**Notes:** —

---

### TC-MEM-007 – Submit application with same sponsor twice
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| (Same session, phone fixed to valid) | — | — |
| Fill | spinbutton[Sponsor 1] ref=e84 → `1` | — |
| Fill | spinbutton[Sponsor 2] ref=e87 → `1` (same as Sponsor 1) | — |
| Fill | textbox[Email] ref=e39 → `test.sponsor@example.com` | — |
| Click | button[Submit Application] ref=e89 | ~100 |

**Result:** Alert ref=e96: "Sponsor 1 and Sponsor 2 must be different members." Form not submitted.  
**Notes:** —

---

### TC-MEM-008 – Deny an application
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Login | committee@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:57539/membership/applications/4 (Iris Submitted) | — |
| Select | combobox[New Status] ref=e92 → `Denied` | ~200 |
| Click | button[Submit Decision] ref=e93 | ~100 |

**Result:** Alert ref=e94: "Application denied." Status → **Denied**. "No further status transitions are available." No member account created.  
**Notes:** —

---

### TC-MEM-009 – Change application status to OnHold
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/membership/applications/3 (Henry Waitlist) | — |
| Select | combobox[New Status] ref=e92 → `OnHold` | ~200 |
| Click | button[Submit Decision] ref=e93 | ~100 |
| Verify | Navigate /membership/applications, filter by OnHold | — |

**Result:** Alert ref=e94: "Application status changed to OnHold." Status → **OnHold**. Dropdown transitions updated (OnHold no longer option, Submitted/Waitlisted/Accepted/Denied available).  
**Notes:** Used Henry Waitlist (app 3) which was in Waitlisted state; transition to OnHold available.

---

### TC-MEM-010 – Accepted application is terminal state
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/membership/applications/1 (Frank Pending, Accepted) | — |
| Verify | No "Make Decision" section rendered | — |

**Result:** Page shows "No further status transitions are available for this application." No decision form or Submit Decision button rendered.  
**Notes:** —

---

## TC-TEE: Tee Time Reservations

### TC-TEE-001 – View tee time availability (Shareholder)
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Login | shareholder1@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:57539/teetimes | — |
| Fill | textbox[Select Date] ref=e38 → `2026-04-21` | ~100 |
| Click | button[Refresh] ref=e40 | ~100 |

**Result:** Tee time grid loaded for 2026-04-21. 7:00 AM slot shows four `+` (clickable book) links. Legend shows Available/Not available/Full indicators. Date/Week toggle present.  
**Notes:** Today's slots (2026-04-20) all show "Not available at this time for your membership" due to PastSlotRule — current time is past operating hours. Tomorrow's slots are bookable.

---

### TC-TEE-002 – View tee time availability (Silver – restricted)
**Status:** ⏭️ SKIPPED  
**Notes:** Requires manual DB setup — default seed gives all levels full access.

---

### TC-TEE-003 – Book a tee time (solo, Shareholder)
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| (From /teetimes, date=2026-04-21) | — | — |
| Click | link[+] ref=e2387 → /teetimes/book?date=2026-04-21&time=07:00 | ~100 |
| Verify | Booking Member: SH-0001 - Alice Shareholder | — |
| Click | button[Book Tee Time] ref=e3554 | ~100 |

**Result:** "Tee time booked successfully for Tuesday, April 21, 2026 at 7:00 AM." Link to View Reservations shown. Reservation ID=1.  
**Notes:** Solo booking — no additional players.

---

### TC-TEE-004 – Book a tee time with foursome
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/teetimes/book?date=2026-04-21&time=09:30 | — |
| Verify | Booking Member: SH-0001 - Alice Shareholder | — |
| Click | button[+ Add Player] ref=e46 | ~100 |
| Select | combobox[Player 2] ref=e54 → `SH-0002 - Bob Shareholder` | ~200 |
| Click | button[+ Add Player] ref=e46 | ~100 |
| Select | combobox[Player 3] ref=e60 → `SH-0003 - Carol Shareholder` | ~200 |
| Click | button[+ Add Player] ref=e46 | ~100 |
| Select | combobox[Player 4] ref=e66 → `SV-0004 - Diana Silver` | ~200 |
| Click | button[Book Tee Time] ref=e49 | ~100 |

**Result:** "Tee time booked successfully for Tuesday, April 21, 2026 at 9:30 AM." Reservation ID=2 with 4 players (Alice, Bob, Carol, Diana).  
**Notes:** Used 9:30 AM slot (>2 hours after 7:00 AM) to satisfy DuplicateBookingRule 2-hour conflict window for Alice.

---

### TC-TEE-005 – Attempt 5th participant (over limit)
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/teetimes/book?date=2026-04-21&time=09:30 | — |
| Click | button[+ Add Player] 3× to add Players 2, 3, 4 | ~100 each |
| Select | Players 2, 3, 4 via combobox | ~200 each |
| Observe | button[+ Add Player] no longer rendered | — |

**Result:** After selecting 3 additional players (4 total including booking member), the `+ Add Player` button disappears from the DOM — no 5th player can be added.  
**Notes:** Enforced by UI hiding the button at max capacity (4 players). No server-side error needed.

---

### TC-TEE-006 – Attempt to double-book the same slot
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/teetimes/book?date=2026-04-21&time=07:00 | — |
| Verify | Booking Member: SH-0001 - Alice Shareholder | — |
| Click | button[Book Tee Time] ref=e52 | ~100 |

**Result:** Error ref=e54: "Unable to create booking. The slot may be full or booking rules prevent this booking." Booking rejected.  
**Notes:** DuplicateBookingRule (2-hour conflict window) blocks Alice from booking 7:00 AM again while she has an existing booking at that time.

---

### TC-TEE-007 – Silver member cannot book restricted slot
**Status:** ⏭️ SKIPPED  
**Notes:** Requires manual DB setup — see TC-TEE-002.

---

### TC-TEE-008 – Cancel a reservation
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/teetimes/my | — |
| Verify | Row: Apr 21, 2026 / 7:00 AM / 1 player / Upcoming (ref=e63) | — |
| Click | button[Cancel] ref=e71 (7:00 AM row) | ~100 |
| Verify | Confirmation prompt: button[Confirm] ref=e83, button[No] ref=e84 | — |
| Click | button[Confirm] ref=e83 | ~100 |

**Result:** Alert ref=e85: "Booking cancelled successfully." 7:00 AM reservation removed from list. Only 9:30 AM foursome (ID=2) remains.  
**Notes:** Two-step cancel with inline confirm/no buttons.

---

### TC-TEE-009 – Admin creates reservation for a member
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Login | admin@clubbaist.com / Pass@word1 | — |
| Navigate | https://localhost:57539/teetimes/staff | — |
| Click | button[Select] ref=e121 (Diana Silver row) | ~100 |
| Verify | Reservations for Diana Silver (SV-0004) panel shown | — |
| Click | button[Book on Behalf] ref=e125 | ~100 |
| Fill | textbox[Select Date] ref=e171 → `2026-04-21` | — |
| Click | button[Refresh] ref=e173 | ~100 |
| Click | link[+] ref=e1355 → /teetimes/book?date=2026-04-21&time=07:00&bookFor=4 | ~100 |
| Verify | combobox[Booking Member (Admin Override)] ref=e2519 → SV-0004 Diana Silver pre-selected | — |
| Click | button[Book Tee Time] ref=e2526 | ~100 |

**Result:** "Tee time booked successfully for Tuesday, April 21, 2026 at 7:00 AM." Link: "Back to Staff Console". Reservation created for Diana Silver by admin.  
**Notes:** Admin booking uses `bookFor=4` query param. Admin sees booking member override dropdown, not the regular member paragraph.

---

### TC-TEE-010 – Standing tee time request
**Status:** ⏭️ SKIPPED  
**Notes:** UI not yet implemented. See TODO-standing-tee-times.md.

---

### TC-TEE-011 – Bronze member cannot book outside restricted hours
**Status:** ⏭️ SKIPPED  
**Notes:** Requires manual DB setup — default seed gives full access.

---

## TC-ADMIN: Administration

### TC-ADMIN-001 – Admin views user list
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| (Logged in as admin@clubbaist.com) | — | — |
| Navigate | https://localhost:57539/admin/users | — |
| Verify | table[User Management] ref=e51 | — |

**Result:** Page loaded with all 8 users: admin@clubbaist.com (Admin), bronze@clubbaist.com (BR-0005), committee@clubbaist.com (MembershipCommittee), frank.pending@example.com (AS-0006), shareholder1–3 (SH-0001–3), silver@clubbaist.com (SV-0004). Each row shows Email, Roles, Member#, Manage/Edit Member links.  
**Notes:** —

---

### TC-ADMIN-002 – Admin changes a member's membership level
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/admin/users | — |
| Click | link[Edit Member] ref=e139 (silver@clubbaist.com row) | ~100 |
| Navigate | /admin/members/4 | — |
| Verify | combobox[Membership Level] ref=e182 → `Silver` (selected) | — |
| Select | combobox[Membership Level] ref=e182 → `Bronze` | ~200 |
| Click | button[Save Changes] ref=e183 | ~100 |

**Result:** Alert ref=e185: "Member profile updated successfully." Member number changed from SV-0004 to BR-0004. Membership Level dropdown now shows Bronze selected.  
**Notes:** Diana Silver is now a Bronze member (BR-0004).

---

### TC-ADMIN-003 – Create a new golf season
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/admin/seasons | — |
| Verify | Existing: 2026 Season, Jan 1 – Dec 31, 2026 | — |
| Click | button[Add Season] ref=e40 | ~100 |
| Fill | textbox[Season Name] ref=e68 → `2027 Season` | — |
| Fill | textbox[Start Date] ref=e71 → `2027-05-01` | — |
| Fill | textbox[End Date] ref=e74 → `2027-10-31` | — |
| Click | button[Add Season] ref=e76 (submit) | ~100 |

**Result:** Alert ref=e79: `Season "2027 Season" added successfully with tee time slots generated.` Table now shows 2027 Season (May 1 – Oct 31, 2027) and 2026 Season.  
**Notes:** Slot generation happens automatically on season creation.

---

### TC-ADMIN-004 – Admin has access to all admin pages
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/admin/users | — |
| Navigate | https://localhost:57539/admin/seasons | — |
| Navigate | https://localhost:57539/teetimes/staff | — |
| Navigate | https://localhost:57539/membership/applications | — |

**Result:** All four admin/staff pages loaded without errors or redirects. Nav shows Staff Console, User Management, Seasons, Applications links for admin role.  
**Notes:** —

---

## TC-NEG: Negative / Edge Cases

### TC-NEG-001 – Login with non-existent email
**Status:** ✅ PASS  
**Elements interacted:**
| Step | Selector / Element | Delay (ms) |
|---|---|---|
| Navigate | https://localhost:57539/Account/Login | — |
| Fill | textbox[Email] ref=e30 → `nobody@nowhere.com` | — |
| Fill | textbox[Password] ref=e32 → `WrongPassword1!` | — |
| Click | button[Log in] ref=e37 | ~100 |

**Result:** Stayed on `/Account/Login`. alert ref=e26: `"Error: Invalid login attempt."` No authentication.  
**Notes:** —

### TC-NEG-002 – Application with blank required field
**Status:** 🔄  
**Result:** —

### TC-NEG-003 – Application using email of existing member
**Status:** 🔄  
**Result:** —

### TC-NEG-004 – Book tee time in the past
**Status:** 🔄  
**Result:** —

### TC-NEG-005 – Non-admin navigates to season management
**Status:** 🔄  
**Result:** —

### TC-NEG-006 – Re-approve already Accepted application
**Status:** 🔄  
**Result:** —

### TC-NEG-007 – Re-deny already Denied application
**Status:** 🔄  
**Result:** —

---

## End-to-End Smoke Test

| Step | Test Case | Account | Action | Result |
|---|---|---|---|---|
| 1 | TC-AUTH-001 | admin@clubbaist.com | Log in as admin | — |
| 2 | TC-ADMIN-003 | admin@clubbaist.com | Create / verify 2026 season | — |
| 3 | — | — | Log out / log in as shareholder3 | — |
| 4 | TC-MEM-003 | shareholder3@clubbaist.com | Submit Sam Tester application | — |
| 5 | — | — | Log out / log in as committee | — |
| 6 | TC-MEM-001 | committee@clubbaist.com | Verify Sam Tester in inbox | — |
| 7 | TC-MEM-002 | committee@clubbaist.com | Approve Sam Tester | — |
| 8 | TC-AUTH-001 | sam.tester@example.com | Log in as new member | — |
| 9 | TC-TEE-003 | sam.tester@example.com | Book solo tee time | — |
| 10 | TC-TEE-008 | sam.tester@example.com | Cancel reservation | — |
| 11 | — | — | Log out / log in as admin | — |
| 12 | TC-TEE-009 | admin@clubbaist.com | Create reservation via staff console | — |

---

## Failures & Screenshots

<!-- Failures appended here during test run -->
