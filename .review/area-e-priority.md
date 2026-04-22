# Area E: Permissions & Authorization — Prioritized Task List

## Critical (security / access control bugs)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| C1 | Add `Clerk` and `ProShopStaff` Identity roles to `AppRoles.cs`, seed them in `AppDbContextSeed.cs`, and re-gate `ScoreConsole.razor` (`/scores/staff`) and `StaffConsole.razor` (`/teetimes/staff`) to `Admin\|Clerk` and `Admin\|ProShopStaff` respectively | H | M | Eliminates the least-privilege violation where front-desk and pro-shop staff must be given full `Admin` access. This is the key unblock from the brief. |
| C2 | Fix `EditMember.razor` so that `HandleSubmitAsync` calls `UserManager.AddClaimAsync` / `RemoveClaimAsync` for `BookStandingTeeTime` (and `copper-tier` when Copper is added) immediately after `DbContext.SaveChangesAsync()` — wrap both in a transaction or compensate on failure | H | S | Today a membership-level change silently desynchronises the Identity claim store from the domain table. A downgraded Shareholder retains booking access indefinitely. |
| C3 | Add a `BookStandingTeeTime` claim panel to `EditUser.razor` — a plain checkbox or `<FluentSwitch>` per claim, with a "Save Claims" action that calls `UserManager.AddClaimAsync` / `RemoveClaimAsync` | H | S | There is currently no in-app path to grant or revoke the standing-tee-time claim; it is seed-only. Admins must use direct database access. |

---

## High Priority (missing roles / claim lifecycle)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| H1 | Add `Clerk` and `ProShopStaff` role toggle rows to the Roles card in `EditUser.razor` (requires C1 to be done first) | H | XS | Once the roles exist in `AppRoles`, the UI must expose them; otherwise the new roles can only be assigned via SQL. |
| H2 | Implement `IMemberClaimSynchroniser` service and wire it into the membership-level change path (called from `EditMember.HandleSubmitAsync` and the member-approval workflow) | H | M | Replaces the ad-hoc fix in C2 with the principled, reusable design prescribed by the design report. Ensures new trigger points (annual renewal, bulk reassignment) all call the same service. |
| H3 | Seed a `Clerk` test user and a `ProShopStaff` test user in `AppDbContextSeed.cs` | M | XS | Without seed users, the new roles cannot be exercised in development or integration tests. |
| H4 | Seed the Copper / Social membership level (`"CP"`) in `AppDbContextSeed.cs` and seed at least one Copper test user | M | XS | Copper is entirely absent from the system. Without a seeded level, the enforcement path (C2 / H2) cannot be tested and the protection against copper-member booking is purely accidental (zero availability windows). |
| H5 | Add explicit Copper-member booking guard: after `IMemberClaimSynchroniser` is in place, add a `copper-tier` claim check in `BookingService.CreateBookingAsync` (or a named policy) so the block is intentional design rather than a configuration side-effect | H | S | Current protection relies on zero availability windows being configured — a misconfigured window silently grants Copper access. An explicit claim check is independent of configuration state. |
| H6 | Add `[Authorize(Policy = PolicyNames.AdminOrClerk)]` to `RecordScore.razor` and `ScoreConfirmation.razor`, replacing `Roles = AdminOrMember` — members should not self-submit scores without a separate clerk-role path | M | XS | Business requirement: score entry is a clerk function. Currently any member can reach the score-entry UI. Depends on C1 and H8. |

---

## Medium Priority (design / dead code cleanup)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| M1 | Remove the `AppRoles.Shareholder` role constant; replace with `AppRoles.MembershipFacts.Shareholder` claim constant in a new inner class | M | XS | The constant is dead code — never seeded as an Identity role, never used in any `[Authorize]` attribute. Its presence implies incomplete infrastructure and misleads future developers. |
| M2 | Remove or retire the dead named policies (`"Admin"`, `"MembershipCommittee"`, `"Member"`) from `Program.cs`, OR convert all call sites to use them (see M3) — the two halves must not coexist | M | XS | Registered policies that are never referenced in `[Authorize]` attributes are dead configuration. They create the false impression that policy-based auth is in use. |
| M3 | Introduce a `PolicyNames` static class and migrate all `[Authorize(Roles="...")]` page attributes to `[Authorize(Policy=PolicyNames.Xyz)]`; define composite policies (`AdminOrClerk`, `AdminOrProShop`, `MemberWithStandingBooking`, `MemberAny`) in `Program.cs` | M | L | Centralises all access-control rules; enables combined role+claim policies (required for `BookStandingTeeTime`); makes mistyped policy names fail at startup. Can be done page-by-page. Prerequisite: M2 resolved first, choose one direction. |
| M4 | Remove `AppRoles.AdminOrCommittee` and `AppRoles.AdminOrMember` composite string constants once M3 is complete and no call site references them | L | XS | Composite role strings are a workaround for the absence of named policies; they become redundant once M3 is done. |
| M5 | Add a comment to `Apply.razor` (`/membership/apply`) documenting that public access is intentional (public application form), and add a note in `Program.cs` or the router config that no global fallback policy is set — new undecorated pages default to public | M | XS | Without documentation, the absence of `[Authorize]` looks like an oversight to any future reviewer, and the latent risk of accidentally-public new pages is unmitigated. |
| M6 | Fix the fragile hardcoded member IDs (`1`, `4`) in `SeedPastBookingsAsync` — resolve them by email lookup or named constant after seeding | L | XS | If seeding order or EF ID generation changes, past bookings silently point to wrong or missing members. |
| M7 | Add a seeded `Associate` test user (level `AS`) — the level is seeded but no test user holds it | L | XS | Associate coverage gap: all-day access and no standing-tee-time claim is currently untested end-to-end. |

---

## Low Priority (polish)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| L1 | Migrate `UserManagement.razor` from QuickGrid + Bootstrap table to `<FluentDataGrid>` with `<FluentPersona>`, role `<FluentBadge>` columns (Admin=red, MembershipCommittee=blue, Member=green, Clerk=teal, ProShopStaff=orange), a claims badge row, and `<FluentSelect>` filters for role and membership level | M | L | Improves scannability so admins can see claim state at a glance without opening each user. Depends on C1, C3. |
| L2 | Migrate `EditUser.razor` to Fluent UI: `<FluentPersona>` header, `<FluentSwitch>` per role, inline claims panel, `<FluentDialog>` for password reset and account delete, account lock/unlock surface | M | L | Consolidates all identity management onto one well-structured page. Depends on C1, C3. |
| L3 | Migrate `EditMember.razor` to Fluent UI: `<FluentCard>` sections, `<FluentSelect>` for membership level with `PreviewClaimImpact()` inline `<FluentMessageBar>` warning before save | M | M | Makes claim-impact visible to admins before they commit a level change. Depends on C2/H2. |
| L4 | Build a new `/admin/roles-claims` surface with a `<FluentDataGrid>` claims audit (filterable by claim presence), bulk grant/revoke actions wrapped in `<FluentDialog>` confirmations, and a role-reference card | M | XL | Useful for season-renewal bulk operations. Not blocking any current workflow; purely operational convenience. |
| L5 | Add account lock/unlock (`LockoutEnabled` / `LockoutEnd`) to `EditUser.razor` — currently an admin cannot suspend a user without deleting them | M | S | ASP.NET Identity supports this natively; it is simply not surfaced. |
| L6 | Standardise the policy naming convention across `Program.cs`: adopt `Policy.Xyz` prefix (kebab-scope permissions stay as-is under `AppRoles.Permissions`) to eliminate the current mix of PascalCase role names and kebab-case permission strings in the same policy registry | L | XS | Cosmetic consistency; prevents confusion when the registry grows. Part of M3 if done together. |
| L7 | Add a symmetric warning in `EditUser.razor` when the `Member` role is added but no member profile exists (a warning already exists for the reverse direction — removing Member when a profile exists) | L | XS | Minor UX consistency gap; easy fix once the roles panel is being touched for C1/H1. |

---

## Grouped Tasks (must go together)

**Group A — Clerk/ProShopStaff role bootstrap (do in one PR)**
- C1 (add role constants + seed + re-gate pages)
- H1 (add role toggles to EditUser)
- H3 (seed test users)

Rationale: The roles are useless without assignment UI, and the UI is untestable without seed users. Splitting across PRs leaves the system in a broken intermediate state where roles exist but cannot be assigned.

**Group B — Claim lifecycle (do in sequence, can be one or two PRs)**
- C3 (manual claim panel on EditUser — immediate fix, no service layer)
- C2 (claim sync in EditMember.HandleSubmitAsync — closes the silent-drift bug)
- H2 (IMemberClaimSynchroniser service — replaces C2's ad-hoc fix with the principled abstraction)

Rationale: C3 provides immediate admin relief. C2 closes the active security gap. H2 is the clean-up that makes the pattern extensible. Do not ship H2 without first removing C2's ad-hoc code to avoid double-application of claims.

**Group C — Copper tier (do in one PR)**
- H4 (seed Copper level and test user)
- H5 (explicit booking guard)

Rationale: Adding the seed without the guard leaves protection accidental. Adding the guard without the seed makes it untestable.

**Group D — Policy standardisation (can be its own epic, do M2 + M3 together)**
- M2 (remove or commit to dead policies)
- M3 (PolicyNames class + migrate call sites)
- M4 (remove composite string constants)
- L6 (naming convention cleanup)

Rationale: M2 and M3 are mutually dependent — you must pick one direction before touching any page attribute. M4 and L6 are cleanup that follows naturally.

---

## Independent Tasks (can be parallelised)

The following tasks have no dependency on any group above and can be picked up by separate engineers concurrently:

| Task | Depends on |
|---|---|
| M1 — Remove `AppRoles.Shareholder` role constant | Nothing; the constant is unused |
| M5 — Document `Apply.razor` intentional public access | Nothing |
| M6 — Fix hardcoded member IDs in seed | Nothing |
| M7 — Seed Associate test user | Nothing |
| L5 — Account lock/unlock surface in EditUser | Nothing (ASP.NET Identity native) |
| L7 — Symmetric warning when adding Member role with no profile | Nothing (small EditUser touch) |
| H6 — Re-gate RecordScore + ScoreConfirmation | Depends only on C1 (roles must exist) and M3 (if policy approach is chosen) — can start immediately after C1 |
