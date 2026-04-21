# Area E: Permissions & Authorization — UI Analysis & Fluent UI Redesign

## Current UI Issues

### 1. EditMember.razor (`/admin/members/{MemberId:int}`)

**What can be changed:**
- Personal information: FirstName, LastName, DateOfBirth, Phone, AlternatePhone, Address, PostalCode
- Membership: MembershipLevelId (rendered as a plain `<InputSelect>` dropdown bound to `_model.MembershipLevelId`)

**Critical gap — membership level change does not update claims:**
`HandleSubmitAsync` sets `_member.MembershipLevel = newLevel` and calls `DbContext.SaveChangesAsync()`. It never touches `UserManager`, `ClaimsIdentity`, or any ASP.NET Identity store. If a member is upgraded from Copper to Gold (which would entitle them to `BookStandingTeeTime`), the claim is never added. Conversely, a downgrade from Gold to Copper does not revoke the claim. The database row and the identity claim diverge silently with no warning to the admin.

**No claim visibility at all:** The form has zero display of what claims the associated user currently holds. An admin has no way to tell from this page whether `BookStandingTeeTime` is set or not.

**No navigation to claims from this page:** There is a "Back to User" link, but EditUser itself also has no claim panel (see below).

---

### 2. UserManagement.razor (`/admin/users`)

**What is shown:**
- Email, Roles (as colored badges), Member # (or dash), Actions column
- Filter by email (client-side, debounced 300 ms)
- "Add User" button linking to `/admin/users/create`

**Actions per row:**
- "Manage" → `/admin/users/{UserId}` (EditUser)
- "Edit Member" → `/admin/members/{MemberShipInfoId}` (only shown when a member profile exists)

**Gaps:**
- No inline role toggle; roles are display-only on this list — an admin must navigate into EditUser to change them.
- No claim column. There is no way to see at a glance which users hold `BookStandingTeeTime`.
- No `Clerk` or `ProShopStaff` role appears anywhere in the role badge set. Users who need those responsibilities must today be granted full `Admin`, which is excessive.
- No bulk actions (e.g., bulk role assignment or bulk claim grant for a class of members).
- No filter by role or membership level — the only filter is free-text email search.
- QuickGrid is used (Microsoft.AspNetCore.Components.QuickGrid), not Fluent UI DataGrid; column sorting only works on Email.
- The "Add User" link goes to `/admin/users/create`, which is a separate page not included in scope; if it duplicates the pattern of EditUser it likely has the same claim omission.

---

### 3. EditUser.razor (`/admin/users/{UserId:guid}`)

**What is shown / editable:**
- Account card: Name, Email, User ID — **read-only display only**, not editable. There is no way to change a user's email or name from this page.
- Roles card: Three checkboxes — Admin, MembershipCommittee, Member — with a "Save Roles" button.
- Reset Password card: Two password fields + button (admin force-reset, no token required).
- Member Profile card: Shows membership number and link to EditMember, or a "Create Member Profile" link if none exists.

**Gaps:**
- `Clerk` and `ProShopStaff` roles are completely absent from the role checkbox list. Only Admin, MembershipCommittee, and Member are offered. Anyone needing clerk-level access must be made a full Admin.
- No claims panel. `BookStandingTeeTime` (and any other future custom claims) cannot be viewed or modified from this page at all.
- The "Create Member Profile" link points to `/admin/users/create`, not to a dedicated "attach member to existing user" flow, which appears to be wrong or at least confusing.
- Account info (email/name) is read-only; an admin cannot correct a misspelled name without going to EditMember.
- No audit log or last-login timestamp.
- No account lock/unlock control (ASP.NET Identity supports `LockoutEnabled` and `LockoutEnd`; neither is surfaced).
- Warning shown when removing Member role ("member record will remain") is useful, but there is no symmetric warning when adding the Member role without a member profile existing.

---

### 4. Missing UI Surfaces (not present anywhere)

| Missing Surface | Impact |
|---|---|
| `BookStandingTeeTime` claim grant/revoke | No admin can set this claim today without raw database access |
| `Clerk` / `ProShopStaff` role assignment | These roles do not exist in AppRoles; affected staff must be Admin |
| Claim-aware membership level change | Upgrading/downgrading a member never syncs claims |
| Account lockout management | Cannot suspend a user without deleting them |
| Copper-member access blocking | Zero UI enforcement; accidental protection only via empty availability windows |
| Audit / activity log | No record of who changed what or when |
| Staff account creation workflow | No guided "create Clerk account" flow distinct from member onboarding |

---

## Fluent UI Blazor Redesign Proposal

### Design Principles
- Replace Bootstrap cards and QuickGrid with Fluent UI Blazor equivalents throughout.
- Keep the three-page structure (list → user detail → member detail) but add a fourth surface for claims management.
- Use `<FluentDialog>` for confirmations and inline editing to reduce full-page navigations.
- Use `<FluentBadge>` and `<FluentPersona>` to make role/claim status immediately scannable in the list.

---

### User Management (`/admin/users`)

**Purpose:** Master list of all identity accounts; entry point to all per-user actions.

**Recommended components:**
- `<FluentDataGrid>` (replaces QuickGrid + Bootstrap table) with server-side sorting and pagination. Columns: Persona, Email, Roles, Claims, Member #, Actions.
- `<FluentPersona>` in the first column — shows initials avatar, full name, and email subtitle.
- `<FluentBadge>` for each role (color-coded: Admin = red, MembershipCommittee = blue, Member = green, Clerk = teal, ProShopStaff = orange).
- A second badge row (lighter, outlined style) for claims: `BookStandingTeeTime` shown as a pill when present.
- `<FluentTextField>` for the email/name search field (replaces raw `<input>`).
- `<FluentSelect>` dropdowns to filter by role and by membership level (new, not present today).
- "Add User" as a `<FluentButton>` (appearance="accent") that opens a `<FluentDialog>` inline rather than navigating away.

**Layout sketch:**
```
┌─────────────────────────────────────────────────────────────┐
│  User Management                          [+ Add User]       │
│  [Search email/name…]  [Role ▼]  [Level ▼]                  │
├──────────────┬────────────┬───────────┬──────────┬──────────┤
│ User         │ Roles      │ Claims    │ Member # │ Actions  │
├──────────────┼────────────┼───────────┼──────────┼──────────┤
│ ○ Jane Smith │ ●Admin     │ StdTee    │ MB-0042  │ [Manage] │
│   jane@…     │            │           │          │          │
├──────────────┼────────────┼───────────┼──────────┼──────────┤
│ ○ Bob Jones  │ ●Member    │ —         │ MB-0107  │ [Manage] │
│   bob@…      │            │           │          │          │
└──────────────┴────────────┴───────────┴──────────┴──────────┘
```

---

### Member Management — EditMember (`/admin/members/{MemberId:int}`)

**Purpose:** Edit the member-profile record (personal data + membership tier). Must also show the resulting claims and trigger claim sync on level change.

**Recommended components:**
- `<FluentCard>` sections replacing Bootstrap `.card` divs.
- `<FluentTextField>` for FirstName, LastName, Phone, AlternatePhone, Address, PostalCode.
- `<FluentDatePicker>` for DateOfBirth (replaces plain `<InputDate>`).
- `<FluentSelect>` for MembershipLevelId (replaces `<InputSelect>`), with an `OnChange` handler that calls a `PreviewClaimImpact()` method.
- A new "Claim Impact" notice area: when an admin changes the membership level, an inline `<FluentMessageBar>` (intent="warning") appears before saving: e.g., "Changing to Gold will grant BookStandingTeeTime. Changing to Copper will revoke it. Save to apply."
- On `HandleSubmitAsync`, after `SaveChangesAsync`, call `UserManager.AddClaimAsync` / `RemoveClaimAsync` as appropriate, inside the same try block.
- `<FluentButton>` (appearance="accent") for Save, wired to disable during submit.

**Layout sketch:**
```
┌─────────────────────────────────────────┐
│  Edit Member  [MB-0042]    [Back to User]│
├─────────────────────────────────────────┤
│  FluentCard: Personal Information        │
│  [FirstName ──────] [LastName ─────────] │
│  [DateOfBirth ─────]                    │
│  [Phone ───────────] [Alt Phone ───────] │
│  [Address ─────────────────] [Postal ──] │
├─────────────────────────────────────────┤
│  FluentCard: Membership                  │
│  Level: [Gold              ▼]            │
│  ⚠ Changing to Copper will revoke        │
│    BookStandingTeeTime claim.            │
├─────────────────────────────────────────┤
│                          [Save Changes]  │
└─────────────────────────────────────────┘
```

---

### EditUser — Manage User (`/admin/users/{UserId:guid}`)

**Purpose:** Identity account detail — roles, claims, password, and link to member profile.

**Recommended changes and components:**
- `<FluentPersona>` at the top (large size) displaying name, email, and User ID.
- Add an "Edit Account" inline section with `<FluentTextField>` for FirstName, LastName, Email (currently these are read-only; email editing should trigger re-confirmation flow).
- **Roles section:** Replace plain checkboxes with `<FluentSwitch>` toggles, one per role. Add `Clerk` and `ProShopStaff` toggles (requires those roles to be added to `AppRoles`). Each toggle shows a description subtitle. Group under a `<FluentCard>` with label "Roles".
- **Claims section (new):** A `<FluentCard>` listing all custom claims. Each claim is a row with a `<FluentSwitch>` (on/off) and a description. Initially: `BookStandingTeeTime` — "Allows booking standing tee times". Saving this section calls `UserManager.AddClaimAsync` / `RemoveClaimAsync`. This is the primary surface for manually overriding claim state independent of membership level.
- **Reset Password:** Wrap the two fields in `<FluentTextField Type="password">` components. Move to a `<FluentDialog>` triggered by a "Reset Password" button to reduce accidental activation.
- **Account Actions section (new):** `<FluentSwitch>` for account lock/unlock (bound to `LockoutEnabled` + `LockoutEnd`). A "Delete Account" `<FluentButton>` (appearance="outline" color=danger) inside a `<FluentDialog>` confirm guard.

**Layout sketch:**
```
┌──────────────────────────────────────────────────┐
│  ○ Jane Smith                    [Back to Users]  │
│    jane@example.com  · ID: 3f2a…                  │
├──────────────────────────────────────────────────┤
│  FluentCard: Roles                                │
│  ◉ Admin           ○ Full system access    [  ◯ ] │
│  ◉ MembershipComm  ○ Review applications   [ ◉ ] │
│  ◉ Member          ○ Tee time access       [ ◉ ] │
│  ◉ Clerk           ○ Front desk ops        [  ◯ ] │  ← NEW
│  ◉ ProShopStaff    ○ Pro shop access       [  ◯ ] │  ← NEW
│                                     [Save Roles]  │
├──────────────────────────────────────────────────┤
│  FluentCard: Claims                   ← NEW PANEL │
│  BookStandingTeeTime                  [ ◉ ]       │
│  "Allows standing tee time booking"               │
│                                    [Save Claims]  │
├──────────────────────────────────────────────────┤
│  FluentCard: Member Profile                       │
│  MB-0042  Jane Smith          [Edit Member Profile]│
├──────────────────────────────────────────────────┤
│  FluentCard: Account Actions                      │
│  Account locked    [  ◯ ]   [Reset Password…]    │
│                             [Delete Account…]     │
└──────────────────────────────────────────────────┘
```

---

### Roles & Claims Management (new surface)

**Recommended route:** `/admin/roles-claims`

**Purpose:** A standalone panel for admins to audit and bulk-manage role memberships and claim assignments across all users — without needing to open individual user pages.

**Why needed:**
- Today there is no way to see all users with `BookStandingTeeTime` at once.
- There is no way to grant/revoke that claim for a batch of users (e.g., after a season renewal).
- There is no record of what claims exist or what they mean.

**Recommended components:**
- Top section: `<FluentDataGrid>` "Claims Audit" — columns: User (FluentPersona), Membership Level, BookStandingTeeTime (FluentBadge or dash), Last Modified. Filterable by claim presence.
- Second section: `<FluentCard>` "Bulk Claim Actions" — `<FluentSelect>` to pick a claim name, `<FluentSelect>` to pick a target membership level, a `<FluentButton>` "Grant to all [level] members" / "Revoke from all [level] members" — each wrapped in a `<FluentDialog>` confirmation showing affected user count.
- Third section: `<FluentCard>` "Role Directory" — static reference table of all `AppRoles` constants with description, so admins understand what each role does. Not editable here; links to individual user pages.

**Layout sketch:**
```
┌──────────────────────────────────────────────────────┐
│  Roles & Claims Management                            │
├──────────────────────────────────────────────────────┤
│  FluentCard: Claims Audit                             │
│  [Filter: show only users with BookStandingTeeTime ▼] │
│  ┌──────────────┬──────────────┬────────────────────┐ │
│  │ User         │ Level        │ BookStandingTeeTime │ │
│  ├──────────────┼──────────────┼────────────────────┤ │
│  │ ○ J. Smith   │ Gold         │ ● Granted          │ │
│  │ ○ B. Jones   │ Copper       │ — Not set          │ │
│  └──────────────┴──────────────┴────────────────────┘ │
├──────────────────────────────────────────────────────┤
│  FluentCard: Bulk Actions                             │
│  Claim: [BookStandingTeeTime ▼]                       │
│  Level: [Gold                ▼]                       │
│  [Grant to all Gold members]  [Revoke from all Gold]  │
├──────────────────────────────────────────────────────┤
│  FluentCard: Role Reference                           │
│  Admin            — Full system access                │
│  MembershipComm.  — Review applications               │
│  Member           — Tee time access                   │
│  Clerk            — Front desk operations  [ADD ROLE] │
│  ProShopStaff     — Pro shop access        [ADD ROLE] │
└──────────────────────────────────────────────────────┘
```

---

## Notes

- **Claim sync must be transactional.** When `HandleSubmitAsync` in EditMember changes membership level, both `DbContext.SaveChangesAsync()` and the `UserManager` claim operations must succeed or both must be rolled back. Currently the page only does the DB save; a partial failure would leave the DB and identity store inconsistent.
- **`Clerk` and `ProShopStaff` roles do not yet exist** in `AppRoles` (not seen in any of the three files). The role toggle UI described above depends on those constants being added first.
- **`BookStandingTeeTime` is currently invisible** across all three pages. The immediate lowest-effort fix — before any Fluent UI migration — is to add a "Claims" section to `EditUser.razor` with a single `<FluentSwitch>` (or even a plain checkbox) for `BookStandingTeeTime`.
- **Copper member blocking** has no UI component today. A dedicated availability-window guard is the correct enforcement point, but an admin note or `<FluentMessageBar>` on the EditMember page ("Copper members: booking access is controlled by availability windows") would clarify intent to future admins.
- **`<FluentDialog>` for destructive actions** (password reset, account delete, bulk claim revoke) is strongly recommended over inline buttons to reduce accidental changes.
- All three existing pages are `@rendermode InteractiveServer` and use `[Authorize(Roles = AppRoles.Admin)]`; the new surfaces should follow the same pattern.
