# Area E: Permissions & Authorization — Gap Analysis

## Summary

The authorization model is partially implemented but carries several meaningful gaps relative to business requirements. The core role set is too narrow — two business actors (staff clerk and pro shop staff) have no dedicated roles and are silently collapsed onto `Admin`. The `Shareholder` role constant exists in `AppRoles.cs` but is never registered as an Identity role, never seeded, and never used in any `[Authorize]` attribute; instead, the Shareholder distinction is expressed only through a claim. The `Copper` (Social) membership level is absent from the system entirely — it is not seeded as a membership level and no enforcement logic prevents a user assigned to that level from booking. The `Apply.razor` page is publicly accessible with no authentication check, which is likely intentional but undocumented. Naming inconsistencies exist between the policy name for standing tee times and its constant path.

---

## Role Model Analysis

### Roles defined in `AppRoles.cs`

| Constant | Value |
|---|---|
| `Admin` | `"Admin"` |
| `MembershipCommittee` | `"MembershipCommittee"` |
| `Member` | `"Member"` |
| `Shareholder` | `"Shareholder"` |

### Roles registered in Identity (seeded in `AppDbContextSeed.cs`)

Only three roles are created at seed time: `Admin`, `MembershipCommittee`, `Member`. The `Shareholder` constant is defined in `AppRoles` but **never passed to `roleManager.CreateAsync`** and is therefore not a live Identity role.

### Business actors vs implemented roles

| Business Actor | Required Role | Implemented? | Notes |
|---|---|---|---|
| Admin / System | `Admin` | Yes | Fully modelled |
| Membership Committee | `MembershipCommittee` | Yes | Fully modelled |
| Club Member (Gold/Silver/Bronze) | `Member` | Yes | Booking level restrictions are handled via membership-level entity, not role |
| Shareholder (standing tee times) | Claim `standing-tee-time.book` | Partial | Claim exists; `Shareholder` role constant unused and not seeded as a role |
| Staff Clerk (score processing, tee sheet) | None | **Missing** | Collapsed onto `Admin`; clerks get full system access as a side-effect |
| Pro Shop Staff (golf day management) | None | **Missing** | `StaffConsole` page is `[Authorize(Roles = AppRoles.Admin)]`; pro shop staff must be granted `Admin` to access it |
| Copper / Social Member | None | **Missing** | No role, no membership level in seed data, no enforcement |
| Finance Committee | None | **Missing** | No financial pages exist yet, but no role is planned for this actor either |

**Key finding:** Two distinct non-admin operational roles (Clerk and Pro Shop Staff) are described in business requirements but do not exist in the role model. Both functions are gated behind `Admin`, which violates the principle of least privilege.

---

## Page Authorization Audit

| Page (route) | `[Authorize]` attribute | Assessment |
|---|---|---|
| `/teetimes` — `Availability.razor` | `Roles = AdminOrMember` | Acceptable — Copper gap (see below) |
| `/teetimes/book` — `CreateReservation.razor` | `Roles = AdminOrMember` | Acceptable — Copper gap (see below) |
| `/teetimes/my` — `MyReservations.razor` | `Roles = Member` | Correct |
| `/teetimes/staff` — `StaffConsole.razor` | `Roles = Admin` | **Gap** — should be a `Clerk` or `ProShopStaff` role, not `Admin` |
| `/teetimes/standing` — `StandingRequest.razor` | `Policy = BookStandingTeeTime` | Correct |
| `/teetimes/standing/my` — `MyStandingRequests.razor` | `Policy = BookStandingTeeTime` | Correct |
| `/teetimes/{id}` — `ReservationDetail.razor` | `Roles = AdminOrMember` | Correct |
| `/membership/apply` — `Apply.razor` | **None** | Intentional (public form) but carries risk — anyone can submit; no documentation of this design decision exists in code |
| `/membership/applications` — `ApplicationInbox.razor` | `Roles = AdminOrCommittee` | Correct |
| `/membership/applications/{id}` — `ReviewApplication.razor` | `Roles = AdminOrCommittee` | Correct |
| `/scores/record` — `RecordScore.razor` | `Roles = AdminOrMember` | **Gap** — per business req, score entry is a clerk function; `Member` should not self-submit without a separate clerk role |
| `/scores/staff` — `ScoreConsole.razor` | `Roles = Admin` | **Gap** — staff score console should be `Clerk` role, not `Admin` |
| `/scores/confirm` — `ScoreConfirmation.razor` | `Roles = AdminOrMember` | Follows `RecordScore` pattern; inherits same gap |
| `/scores/my` — `MyScoreSubmissions.razor` | `Roles = Member` | Correct |
| `/admin/users` — `UserManagement.razor` | `Roles = Admin` | Correct |
| `/admin/users/create` — `CreateUser.razor` | `Roles = Admin` | Correct |
| `/admin/users/{id}` — `EditUser.razor` | `Roles = Admin` | Correct |
| `/admin/members/{id}` — `EditMember.razor` | `Roles = Admin` | Correct |
| `/admin/seasons` — `SeasonManagement.razor` | `Roles = Admin` | Correct |
| `/admin/standing-teetimes` — `StandingTeeTimes.razor` | `Roles = Admin` | Correct |
| `TeeTimeAvailabilityPanel.razor` (component, no route) | None | Correct — it is a child component, not a routable page |
| `Error.razor` / `NotFound.razor` | None | Correct — error/utility pages are intentionally public |

**Pages with no `[Authorize]` that carry risk:** `Apply.razor` is the only routable page without authorization. The omission is functionally appropriate (public application form) but should be documented with a comment.

---

## Shareholder Claim Issues

### How the claim is granted

The `standing-tee-time.book` permission claim (`AppRoles.Claims.StandingTeeTimeBooking`) is granted in `AppDbContextSeed.cs` at seeding time for any user whose `MembershipLevelShortCode` is `"SH"`. This is a **one-time, seed-only operation**.

### Gaps

1. **No runtime grant/revoke UI.** The `EditUser.razor` page manages only three roles (`Admin`, `MembershipCommittee`, `Member`). There is no checkbox, toggle, or action on that page to add or remove the `standing-tee-time.book` claim. If a Shareholder is downgraded to Associate or Silver, the claim persists in their identity until manually removed via database or code — there is no in-app path to do so.

2. **EditMember can change membership level without touching the claim.** `EditMember.razor` lets an Admin change a member's `MembershipLevel` entity to any level (e.g. from Shareholder to Silver), but this only updates the domain table. The Identity claim is not recalculated or removed. After the change, the user still holds the `standing-tee-time.book` claim and can still access `StandingRequest.razor` and `MyStandingRequests.razor`.

3. **`Shareholder` role constant is dead code.** `AppRoles.Shareholder = "Shareholder"` is defined but never used in any `[Authorize]` attribute, never registered as an Identity role, and never seeded. The standing tee time policy correctly uses the claim instead of this role, but the constant's existence implies it was intended for something that was never completed — or creates confusion about the authorization model.

4. **`Admin/StandingTeeTimes.razor` exists** but only lets Admins view standing tee time requests. There is no UI to approve, deny, or manage claim assignment from that page.

---

## Copper Member Access Enforcement

**Business requirement:** Copper (Social) members have NO golf privileges.

**Current implementation:** There is no Copper membership level anywhere in the system:

- `AppDbContextSeed.cs` seeds four membership levels: `SH` (Shareholder), `SV` (Silver), `BR` (Bronze), `AS` (Associate). Copper/Social is not seeded.
- There is no `"CP"` or `"SO"` short code in `MembershipLevels`.
- No seeded user carries a Copper level.
- `AppRoles.cs` has no Copper constant or claim.

**Consequence:** If a Copper member were created today by assigning them the `Member` role and creating a `MemberShipInfo` record pointing to a membership level with no `MembershipLevelTeeTimeAvailability` rows, `MembershipLevelAvailabilityRule` would return `SpotsRemaining = -1` for every slot (no availability windows match), effectively blocking bookings at the service layer. However:

- The `Availability.razor` and `CreateReservation.razor` pages allow access (`Roles = AdminOrMember`) — a Copper member assigned the `Member` role would reach the booking UI.
- The block would occur at `BookingService.CreateBookingAsync` only, not at page authorization level.
- There is no `[Authorize]` policy that explicitly denies Copper members before they interact with the UI.
- Since Copper is not seeded, this scenario cannot be tested, and the enforcement path is untested.

**Gap summary:** Copper is unimplemented — no level, no seeded user, no explicit UI-level enforcement. The service layer would block bookings as a side-effect of having zero availability windows, but this is accidental protection, not intentional design.

---

## Membership Level vs Role Gap

Booking time restrictions (Gold/Silver/Bronze windows) are enforced via the **entity model**, not via roles or claims:

- `MembershipLevel` has a `List<MembershipLevelTeeTimeAvailability>` navigation property.
- `MembershipLevelAvailabilityRule` queries this table to accept or reject a booking.
- The seed data correctly populates availability windows for SV and BR; SH and AS get all-day access (the `default` case in `AddAvailabilities`).

This is the correct architectural choice — enforcing time windows through roles would require an impractical number of role/policy combinations.

**However, the approach has one gap:** The `Member` role is a flat, single-value role. There is no sub-role or claim that distinguishes Gold, Silver, Bronze, or Copper at the Identity layer. This means:

- A user cannot be redirected or shown different UI based on their tier from the `AuthenticationState` alone.
- If the `MemberShipInfo` entity relationship is not loaded, there is no fallback tier indicator in the claims principal.
- Changing a member's level in `EditMember.razor` takes effect in the domain table immediately, but the user's current session claims do not reflect this until they re-authenticate.

---

## Seed Data Analysis

### Seeded roles

`Admin`, `MembershipCommittee`, `Member` (correct; `Shareholder` role is not seeded — see above).

### Seeded membership levels

`SH` (Shareholder), `SV` (Silver), `BR` (Bronze), `AS` (Associate). **Missing: Copper / Social.**

### Seeded users

| Email | Role | Membership Level | Standing Claim |
|---|---|---|---|
| `admin@clubbaist.com` | Admin | None | No |
| `committee@clubbaist.com` | MembershipCommittee | None | No |
| `shareholder1@clubbaist.com` | Member | SH (Shareholder) | Yes |
| `shareholder2@clubbaist.com` | Member | SH (Shareholder) | Yes |
| `shareholder3@clubbaist.com` | Member | SH (Shareholder) | Yes |
| `silver@clubbaist.com` | Member | SV (Silver) | No |
| `bronze@clubbaist.com` | Member | BR (Bronze) | No |

### Coverage gaps

| Scenario | Covered by seed? |
|---|---|
| Admin tee sheet / score console access | Yes (admin@) |
| Membership committee review | Yes (committee@) |
| Shareholder booking (any time) | Yes (shareholder1–3@) |
| Silver booking (restricted windows) | Yes (silver@) |
| Bronze booking (restricted windows) | Yes (bronze@) |
| Associate booking (any time, no standing) | **No** — AS level is seeded as a `MembershipLevel` but no user with AS level is seeded |
| Copper / Social (no golf) | **No** — level not seeded, no user seeded |
| Staff Clerk (score entry, tee sheet) | **No** — no Clerk role or user exists |
| Pro Shop Staff (golf day mgmt) | **No** — no ProShop role or user exists |
| MembershipCommittee reviewing an application | Yes (committee@ + 5 seeded applications) |

**Note on hardcoded member IDs:** `SeedPastBookingsAsync` uses hardcoded member IDs (`1`, `4`) with a comment that these are "sequentially assigned by EF during seeding." This is fragile — if seeding order or EF ID generation changes, the past bookings will silently point to wrong or missing members.

---

## Policy Naming Issues

### Defined policies (in `Program.cs`)

| Policy name | What it requires |
|---|---|
| `"Admin"` | Role = `Admin` |
| `"MembershipCommittee"` | Role = `Admin` or `MembershipCommittee` |
| `"Member"` | Role = `Member` |
| `"standing-tee-time.book"` | Role = `Member` AND Claim `clubbaist.permission` = `standing-tee-time.book` |

### Issues

1. **Policy name reuses role name strings.** The policies `"Admin"`, `"MembershipCommittee"`, and `"Member"` share their names exactly with the role strings. Pages use `[Authorize(Roles = ...)]` directly rather than `[Authorize(Policy = ...)]` for most of these, so the policies defined in `Program.cs` for `Admin`, `MembershipCommittee`, and `Member` are registered but **not actually used anywhere in the page attributes**. They are dead configuration.

2. **`MembershipCommittee` policy is broader than its name suggests.** The policy named `"MembershipCommittee"` grants access to `Admin` OR `MembershipCommittee`. Its name implies committee-only access but the policy includes Admin. This is not wrong functionally but could mislead future developers who read the policy name and assume it maps exactly to the role.

3. **`standing-tee-time.book` is a permission string used as a policy name.** This works because `AppRoles.Permissions.BookStandingTeeTime` is the same string used as both the claim value and the policy name, which is consistent. However the naming convention mixes kebab-case permission-scopes (`standing-tee-time.book`) with PascalCase role names (`Admin`, `MembershipCommittee`) in the same policy registry — no consistent naming convention is applied across policies.

4. **`Shareholder` role constant in `AppRoles.cs` is never used as a policy.** It appears to be planned infrastructure that was never wired up.

---

## Notes

- **`MyReservations.razor` uses `Roles = Member` (not `AdminOrMember`):** Admins cannot view individual member reservation lists from that page. This may be intentional but means an Admin cannot impersonate or inspect a member's booking history from the standard UI.
- **`ScoreConsole.razor` uses `Roles = Admin` while `RecordScore.razor` uses `Roles = AdminOrMember`:** There is a distinction between staff score entry (admin-only console) and member self-service score recording — but the business requirement calls for a separate `Clerk` role for staff score entry, not `Admin`.
- **No `[Authorize]` fallback at the router level:** The app does not set a global `AuthorizeRouteView` fallback policy in `Program.cs`. Undecorated pages are public by default. Currently only `Apply.razor` and the error/not-found pages are undecorated, which is acceptable — but this is a latent risk if new pages are added without authorization attributes.
- **`TeeTimeAvailabilityPanel.razor`** is a child component with no route — the absence of `[Authorize]` is correct; it inherits the parent page's auth context.
