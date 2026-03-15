# UI Planning – Page Catalog (Excluding Built-in ASP.NET Identity Pages)

## Scope and Assumptions
- This planning covers custom product pages only.
- **Built-in ASP.NET Identity pages** (Login, Register, Forgot Password, etc.) are intentionally excluded.
- Page definitions are aligned to the currently modeled use cases in:
  - Membership Applications (UC-MA-01, UC-MA-02)
  - Tee Time Reservations (UC-TT-01, UC-TT-02)

## Authorized User Roles
- **Prospective Member (Applicant)**
- **Membership Admin/Clerk**
- **Membership Committee Member**
- **Member (Gold/Silver/Bronze)**
- **Admin/Clerk (Tee Times)**

---

## 1) Shared / Navigation Pages

| Page | Purpose | Primary UI Format | Authorized users | Key actions |
|---|---|---|---|---|
| Home / Dashboard | Route users to domain-specific actions and show quick status summaries | Card list + summary tiles + alert banner | All authenticated users (role-filtered content) | Navigate to allowed pages; view personal work queue snippets |
| Access Denied / Unauthorized | Explain authorization failures and next steps | Message panel | Any authenticated/unauthenticated user who hits protected route | Return to previous page; navigate to home |

---

## 2) Membership Applications Pages

| Page | Use-case linkage | What is displayed | Primary UI format | Authorized users | Key actions |
|---|---|---|---|---|---|
| New Membership Application | UC-MA-01 Submit Membership Application | Applicant profile, contact details, sponsor details, required declarations/consents | Multi-section **form** with validation summary | Prospective Member (Applicant); optionally Membership Admin/Clerk entering on behalf of applicant | Save draft (optional), submit application, cancel |
| Application Submission Confirmation | UC-MA-01 | Submitted application number, timestamp, next steps | Read-only confirmation panel | Applicant, Membership Admin/Clerk | View application detail, return to dashboard |
| My Membership Application Status | UC-MA-01/02 outcome visibility | Current status (Submitted/Accepted/Denied/OnHold/Waitlisted), latest status date, notes allowed for applicant view | Read-only detail view + compact status timeline | Applicant | View history; optionally download/print summary |
| Membership Application Inbox | UC-MA-02 Review and Decide Membership Application | Filterable list of applications by status/date/review cycle | **Table/grid** with search/filter/sort | Membership Admin/Clerk, Membership Committee Member | Open application, assign/reassign reviewer (if enabled), move to review agenda state |
| Membership Application Review Workspace | UC-MA-02 | Full application detail, sponsor eligibility checks, validation flags, historical decisions/notes | Split layout: read-only detail + decision **form** + status history **table/timeline** | Membership Committee Member; Membership Admin/Clerk (support mode) | Record decision (Accept/Deny/OnHold/Waitlist), add rationale note, save interim review note |
| Membership Decision Audit Trail | UC-MA-02 audit/history | Complete chronological status history with actor/time/reason | **Table** + optional timeline | Membership Committee Member, Membership Admin/Clerk | Filter/export history (optional) |

### Membership authorization notes
- Applicants can create and view their own applications, but cannot perform committee decisions.
- Committee Members can record final decisions.
- Membership Admin/Clerk can prepare records and support workflow, with decision authority configured per policy.

---

## 3) Tee Time Reservations Pages

| Page | Use-case linkage | What is displayed | Primary UI format | Authorized users | Key actions |
|---|---|---|---|---|---|
| Tee Time Availability Search | UC-TT-01 Create Tee Time Reservation | Date selector, available tee slots, occupancy indicator, applicable restrictions message | Search/filter controls + slot **table/list** | Member (Gold/Silver/Bronze), Admin/Clerk (on behalf) | Search slots, inspect slot details, start booking |
| Create Reservation | UC-TT-01 | Selected slot, booking member, additional players, policy checks (season window/time restrictions/capacity) | Guided **form** with inline validation | Member; Admin/Clerk (on behalf) | Confirm reservation, modify party composition, cancel flow |
| Reservation Confirmation | UC-TT-01 | Reservation ID, booked slot, players, policy notices | Read-only confirmation panel | Member, Admin/Clerk | View reservation detail, create another reservation |
| My Reservations | UC-TT-02 Review and Maintain Reservation | Upcoming/past reservations for current member | **Table/list** with filters (date range, status) | Member | Open reservation, edit eligible details, cancel reservation |
| Reservation Detail / Maintenance | UC-TT-02 | Reservation metadata, players, slot info, occupancy impact preview for edits | Read-only detail + edit/cancel **form** | Member (own reservations), Admin/Clerk | Update reservation details, cancel reservation |
| Staff Reservation Console | UC-TT-01/02 staff support | Search member, manage reservations across members, operational flags | Master-detail with searchable **table** and side panel | Admin/Clerk (Tee Times) | Create/edit/cancel on behalf of member, view member booking history snippet |

### Tee-time authorization notes
- Members can only manage their own reservations.
- Admin/Clerk can perform staff-assisted booking and maintenance on behalf of members.
- Policy enforcement (eligibility/time restrictions) should still evaluate the booking member account.

---

## 4) Suggested Initial Build Sequence (UI)
1. Shared Home/Dashboard shell with role-based navigation.
2. Membership: New Application + Submission Confirmation + Application Inbox.
3. Membership: Review Workspace + Decision Audit Trail + Applicant Status page.
4. Tee Times: Availability Search + Create Reservation + Confirmation.
5. Tee Times: My Reservations + Reservation Detail/Maintenance + Staff Console.

This order delivers one end-to-end flow per domain early, then adds operational and audit depth.
