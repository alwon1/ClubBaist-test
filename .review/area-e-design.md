# Area E: Permissions & Authorization — Design Analysis

## Summary

The current authorization model has three structural problems: (1) role constants exist that are never enforced as Identity roles and have no runtime effect; (2) named policies are registered in DI but bypassed everywhere in favour of raw `[Authorize(Roles="...")]` strings, making the policy layer dead code; (3) Shareholder is expressed as an identity role but represents a membership-level fact, causing a conceptual mismatch that makes lifecycle management difficult. In addition, two real operational actors — Clerk and ProShop Staff — have no dedicated role, so they fall back to Admin, which grants excessive access. The `BookStandingTeeTime` claim has no managed lifecycle; it is only ever set at seed time.

The recommended direction is: keep a small, stable set of identity roles for access control; express membership-level facts (Shareholder status, Copper status) as claims; replace every raw `Roles=` string with named policy constants; and add a managed claim-lifecycle service triggered from member-approval and level-change events.

---

## AppRoles Cleanup (keep/rename/remove)

### Keep (no change)

| Constant | Reason |
|---|---|
| `Admin` | Core administrative role; used to protect StaffConsole, ScoreConsole, etc. |
| `MembershipCommittee` | Distinct access pattern (approve members, view applications); legitimately separate from Admin. |
| `Member` | The principal role for all playing members; broad enough to cover Full, Associate, and Junior tiers. |
| `AdminOrCommittee` | Convenience composite for `[Authorize(Roles=...)]` — keep until policies are standardised; remove once policies replace it. |
| `AdminOrMember` | Same rationale as above. |

### Remove

| Constant | Reason |
|---|---|
| `Shareholder` (role constant) | Shareholder is a membership-level attribute, not an access-control role. It should become a claim (see Shareholder section below). Keeping it as a role constant implies it would be added/removed from the Identity role store when membership level changes, which is heavyweight and mismatches the concept. Remove the constant from `AppRoles` and replace with a claim entry under `AppRoles.Permissions` or a new `AppRoles.MembershipFacts` inner class. |

### Add

| Constant | Reason |
|---|---|
| `Clerk` | New role for front-desk staff who need ScoreConsole and limited StaffConsole access but must not reach admin-only pages (member management, financial config). |
| `ProShopStaff` | New role for pro-shop personnel who need booking-management and inventory access but no financial or membership data. |

### Revised AppRoles sketch

```csharp
public static class AppRoles
{
    public const string Admin              = "Admin";
    public const string MembershipCommittee = "MembershipCommittee";
    public const string Member             = "Member";
    public const string Clerk              = "Clerk";
    public const string ProShopStaff       = "ProShopStaff";

    // Composite helpers — retire once all callers use named policies
    public const string AdminOrCommittee = $"{Admin},{MembershipCommittee}";
    public const string AdminOrMember    = $"{Admin},{Member}";

    public static class ClaimTypes
    {
        public const string Permission     = "clubbaist.permission";
        public const string MembershipFact = "clubbaist.membership";
    }

    public static class Permissions
    {
        public const string BookStandingTeeTime = "standing-tee-time.book";
    }

    public static class MembershipFacts
    {
        public const string Shareholder = "shareholder";
        public const string CopperTier  = "copper-tier";
    }

    public static class Claims
    {
        public static Claim StandingTeeTimeBooking { get; } =
            new(ClaimTypes.Permission, Permissions.BookStandingTeeTime);

        public static Claim ShareholderStatus { get; } =
            new(ClaimTypes.MembershipFact, MembershipFacts.Shareholder);

        public static Claim CopperTierStatus { get; } =
            new(ClaimTypes.MembershipFact, MembershipFacts.CopperTier);
    }
}
```

---

## Policy vs Role Attribute Standardization

### Current state

Named policies (`Admin`, `MembershipCommittee`, `Member`) are registered in `Program.cs` but never referenced in `[Authorize]` attributes. All Razor/Blazor pages use `[Authorize(Roles="Admin")]` or `[Authorize(Roles=AppRoles.AdminOrCommittee)]` directly. The named policies are dead code.

### Tradeoffs

| Approach | Pros | Cons |
|---|---|---|
| `[Authorize(Roles="...")]` | Simple; immediately readable; no DI wiring needed | Hard-codes role names as strings; cannot express claim-based rules; logic is scattered across every page; changing a rule means finding every attribute |
| Named policies (`[Authorize(Policy="...")]`) | Single point of change; can combine roles AND claims in one policy; testable in isolation; policy name is a stable contract | Requires policy registration in `Program.cs`; slightly more indirection |

### Recommendation: standardise on named policies

Move every access-control decision into a named policy registered in `Program.cs`. Pages use only `[Authorize(Policy = PolicyNames.Xyz)]`. Role strings and claim requirements live exclusively in the policy definitions.

Rationale:
- The `BookStandingTeeTime` claim already exists and will be required alongside a role check; `[Authorize(Roles=...)]` cannot express a combined role+claim rule without a policy.
- Adding `Clerk` and `ProShopStaff` roles means composites like `AdminOrClerk` would proliferate as string constants. Policies encapsulate that.
- A single `PolicyNames` static class gives the compiler a contract; mistyped policy names fail at startup (during `app.Build()` validation) rather than silently allowing access.

Define a companion `PolicyNames` class:

```csharp
public static class PolicyNames
{
    public const string Admin              = "Policy.Admin";
    public const string AdminOrCommittee   = "Policy.AdminOrCommittee";
    public const string AdminOrClerk       = "Policy.AdminOrClerk";
    public const string MemberWithBooking  = "Policy.MemberWithBooking";
    public const string MemberAny          = "Policy.MemberAny";
    public const string ProShopStaff       = "Policy.ProShopStaff";
}
```

The composite helpers `AdminOrCommittee` and `AdminOrMember` in `AppRoles` can be removed once every call site is migrated to `[Authorize(Policy=...)]`.

---

## Shareholder Claim vs Role Analysis

### Why it was modelled as a role

Shareholder was likely added to `AppRoles` because ASP.NET Identity's role system is the most familiar extension point. A role constant gets added, seeded, and checked the same way as `Admin` or `Member`.

### Why a role is wrong here

1. **Cardinality**: A shareholder is still a Member. Adding the `Shareholder` role means the user now has two roles (`Member` + `Shareholder`) and every role check that says "Member" also needs to say "Member,Shareholder" — or worse, checks silently miss shareholders.
2. **Lifecycle coupling**: Shareholder status tracks a financial/legal fact about a membership record. It changes when membership level changes. Identity roles are designed for stable access-control categories, not for facts that need to stay in sync with a domain model.
3. **No distinct page access**: The gap analysis shows Shareholder is never used in `[Authorize]`. It grants no additional page access. Its only purpose is to carry a distinguishing fact into the claims principal — which is precisely what claims are for.

### Claim is the correct model

Replace `AppRoles.Shareholder` with a `clubbaist.membership` claim whose value is `"shareholder"`. The claim is added to the user's claims when their membership level is set to Shareholder, and removed when it changes away. This keeps Identity roles stable and expresses the domain fact where ASP.NET is designed to express it.

A policy can then be written: `policy.RequireClaim(AppRoles.ClaimTypes.MembershipFact, AppRoles.MembershipFacts.Shareholder)` if any page ever needs shareholder-specific gating. Nothing in access control breaks by making this change; everything that currently does nothing continues to do nothing, but is now correctly typed.

---

## Missing Roles Design (Clerk, ProShopStaff)

### Clerk

**Who they are**: Front-desk staff who enter scores, check in members at the first tee, and look up tee-time sheets. They do not manage members, set fees, or access financial reports.

**Current problem**: ScoreConsole and StaffConsole require `Admin`. Clerks are given Admin accounts, granting full system access.

**Design**:
- Add `Clerk` as an Identity role.
- ScoreConsole policy: `RequireRole(Admin, Clerk)` — clerks can score; admins can also score.
- StaffConsole check-in section: same composite.
- Member management, fee configuration, financial reports: remain `Admin`-only.
- No claims required; role membership is sufficient because clerk access is uniform.

### ProShopStaff

**Who they are**: Pro-shop employees who manage tee-time bookings, handle walk-in reservations, and may run the booking console. They do not see member financials or committee materials.

**Current problem**: No role exists; these users presumably also get Admin or are blocked entirely.

**Design**:
- Add `ProShopStaff` as an Identity role.
- Booking management pages: `RequireRole(Admin, ProShopStaff)`.
- Tee-time override/cancellation pages: same composite.
- No access to membership approval, financial config, or score entry unless combined with `Clerk`.
- A single user can hold both `Clerk` and `ProShopStaff` if the club employs multi-role staff.

### Why roles, not claims, for these two

Both Clerk and ProShopStaff represent stable employment-category facts that control which application areas a user can reach. That is exactly what roles are designed for. Claims are better for fine-grained, per-user, or frequently-changing permissions.

---

## Claim Lifecycle Design (BookStandingTeeTime)

### Current problem

`BookStandingTeeTime` is only granted at database seed time. There is no UI or service layer to grant it to a real member when they become eligible, or to revoke it when their membership level changes (e.g., downgrades from Full to Copper).

### Eligibility rule (inferred from gap analysis)

Full members and above (including Shareholders) are eligible to book standing tee times. Copper-tier members are not. Associate and Junior members: club policy unclear — treat as configurable or default to not eligible until policy is confirmed.

### Recommended lifecycle design

**Trigger points** — the claim should be evaluated and synchronised at:

1. Member approval (new member workflow completes, level is set for the first time).
2. Membership level change (admin changes a member's tier).
3. Annual renewal processing (if tier can change on renewal).

**Mechanism**:

Introduce a `MemberClaimSynchroniser` domain service (or application service in the ClubBaist application layer):

```csharp
public interface IMemberClaimSynchroniser
{
    Task SynchroniseAsync(Guid memberId, MembershipLevel newLevel, CancellationToken ct);
}
```

Implementation logic:

```
if newLevel is Full or Shareholder:
    ensure user has BookStandingTeeTime claim
    ensure user does NOT have CopperTier claim
else if newLevel is Copper:
    ensure user does NOT have BookStandingTeeTime claim
    ensure user HAS CopperTier claim
else (Associate, Junior, or unknown):
    ensure user does NOT have BookStandingTeeTime claim
    ensure user does NOT have CopperTier claim
```

The synchroniser is called from the domain event handler for `MembershipLevelChangedEvent` (or directly from the approval service if no event infrastructure exists yet).

**Storage**: ASP.NET Identity's `UserClaim` table (`AspNetUserClaims`) is the correct store. The synchroniser calls `UserManager<AppUser>.RemoveClaimAsync` and `AddClaimAsync`. The claim is then part of the cookie/token on the user's next sign-in.

**Sign-in refresh**: Because claims are baked into the authentication cookie at sign-in, a level change does not take effect until the user's next sign-in (or security stamp refresh). This is standard ASP.NET Identity behaviour. If immediate revocation is required (e.g., Copper downgrade mid-session), update the security stamp (`UpdateSecurityStampAsync`) which forces cookie re-validation on the next request.

**No admin UI for raw claim grant/revoke**: Do not expose a generic "add permission claim" UI. Admins change membership level; the synchroniser handles claims as a consequence. This prevents the claim store drifting from the membership record.

---

## Recommended Authorization Model

### Roles (identity roles — who you are employed/enrolled as)

| Role | Pages / Areas |
|---|---|
| `Admin` | Everything |
| `MembershipCommittee` | Member applications, approval queue, committee reports |
| `Clerk` | ScoreConsole, StaffConsole (check-in section only) |
| `ProShopStaff` | Booking management, tee-sheet management, walk-in reservations |
| `Member` | Member portal, tee-time booking, own profile, own handicap |

### Claims (per-user facts — what you are specifically allowed or what you are)

| Claim type | Claim value | Meaning | Set by |
|---|---|---|---|
| `clubbaist.permission` | `standing-tee-time.book` | May book standing/recurring tee times | MemberClaimSynchroniser on level change |
| `clubbaist.membership` | `shareholder` | Is a shareholder-level member | MemberClaimSynchroniser on level change |
| `clubbaist.membership` | `copper-tier` | Is a copper-tier member | MemberClaimSynchroniser on level change |

### Policies (named, registered in Program.cs)

| Policy name | Rule |
|---|---|
| `Policy.Admin` | Role: Admin |
| `Policy.AdminOrCommittee` | Role: Admin or MembershipCommittee |
| `Policy.AdminOrClerk` | Role: Admin or Clerk |
| `Policy.AdminOrProShop` | Role: Admin or ProShopStaff |
| `Policy.MemberAny` | Role: Member (any tier) |
| `Policy.MemberWithStandingBooking` | Role: Member AND claim `standing-tee-time.book` |
| `Policy.ShareholderMember` | Role: Member AND claim membership=shareholder |

### What this eliminates

- `AppRoles.Shareholder` role constant — replaced by a claim.
- `AppRoles.AdminOrCommittee` and `AdminOrMember` composite string constants — replaced by policy names.
- Raw `[Authorize(Roles="Admin")]` strings scattered across pages — replaced by `[Authorize(Policy=PolicyNames.Admin)]`.
- Admins acting as Clerks — Clerk gets exactly the access it needs.
- Accidental Copper-member blocking by zero-window availability — replaced by explicit `copper-tier` claim check in the booking service.

---

## Notes

- **Migration path**: Because all current `[Authorize]` attributes use role strings, migration to named policies can be done page-by-page. The named policies can be registered immediately (they will be ignored until a page references them), so `Program.cs` work and page attribute work can be decoupled.
- **Copper blocking**: The current copper-member blocking is a side-effect of how availability windows are configured (zero windows = nothing to book). This is fragile — a misconfigured window accidentally grants a copper member access. The `copper-tier` claim + an explicit policy check (or service-layer guard) makes the intent explicit and independent of configuration state.
- **Shareholder governance**: If shareholders ever need access to a distinct portal section (e.g., equity reports, AGM documents), the `Policy.ShareholderMember` policy is already in place. No code changes needed at that point beyond adding `[Authorize(Policy=PolicyNames.ShareholderMember)]` to the new page.
- **Role vs claim for CopperTier**: CopperTier is deliberately a claim, not a role, because copper members are still members — they hold the `Member` role. The claim is a restriction fact, not a new access category. If copper members ever need a distinct area, reconsider.
- **AppRoles.Claims static properties**: The existing pattern of exposing pre-constructed `Claim` objects as static properties is good — keep and extend it for `ShareholderStatus` and `CopperTierStatus`. It prevents string-typo bugs at the call sites.
