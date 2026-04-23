# Area E: Permissions & Authorization – Design Questions

> **Answer after Area F** — role decisions gate feature implementation in all other areas.

These decisions determine the role model, which gates what gets built in Areas A–D.

**Related review files:** `.review/area-e-gap.md`, `.review/area-e-design.md`, `.review/area-e-ui.md`, `.review/area-e-priority.md`

---

## Question 5 – Staff roles

Should we introduce **separate `Clerk` and `ProShopStaff` roles** for staff console and score-entry access, or continue **collapsing those actors onto `Admin`**?

Currently:
- `StaffConsole.razor` is `[Authorize(Roles = AppRoles.Admin)]`
- `ScoreConsole.razor` is `[Authorize(Roles = AppRoles.Admin)]`

Granting staff `Admin` gives them full system access (user management, season management, etc.), which violates least-privilege.

- **Option A – Add `Clerk` and `ProShopStaff` roles:** Correct long-term design; requires seeding new roles and users, updating `[Authorize]` attributes on affected pages.
- **Option B – Keep `Admin` for now:** Simpler; defers least-privilege enforcement to a later phase.

**Your answer:**
<!-- e.g. "Option A – add Clerk and ProShopStaff roles" -->

---

## Question 6 – Apply.razor public access & anti-spam

The `/membership/apply` form (`Apply.razor`) has **no `[Authorize]`** attribute — anyone can submit a membership application without logging in.

Should it remain public, or require login? If public, do we want any anti-spam control?

- **Option A – Remain public, no anti-spam:** Current behavior; simplest for applicants who don't yet have an account.
- **Option B – Remain public + add rate limiting:** Protects against bot submissions; requires adding `Microsoft.AspNetCore.RateLimiting` or similar.
- **Option C – Remain public + add CAPTCHA:** Stronger spam protection; requires third-party integration (e.g., hCaptcha, Cloudflare Turnstile).
- **Option D – Require login to apply:** Applicants must register before applying; changes the intended flow.

**Your answer:**
<!-- e.g. "Option A – remain public, no anti-spam for now" -->

---

## Question 7 – Shareholder as Identity role vs. claim-only

Currently `AppRoles.Shareholder = "Shareholder"` is defined but **never seeded as an Identity role** and never used in any `[Authorize]` attribute. The Shareholder distinction is expressed only through the `standing-tee-time.book` permission claim.

Should `Shareholder` be promoted to a **proper Identity role** (seeded, used in `[Authorize]`), or keep the **current claim-only approach**?

- **Option A – Proper Identity role:** Seeded via `roleManager.CreateAsync`; can be used in `[Authorize(Roles = AppRoles.Shareholder)]` directly; simpler to query "is this user a shareholder".
- **Option B – Keep claim-only approach:** Standing tee time policy correctly uses the claim; the role constant can be removed as dead code or kept for future use.

**Your answer:**
<!-- e.g. "Option B – keep claim-only, remove the dead Shareholder role constant" -->

---

## Question 8 – Auto-recalculate claims when membership level changes

Currently, `EditMember.razor` lets an Admin change a member's `MembershipLevel` in the domain table — but **the Identity claims are not recalculated**. If a Shareholder is downgraded to Silver, they still hold the `standing-tee-time.book` claim and can still access standing tee time pages.

Should the associated permission claims be **automatically recalculated/revoked** when an admin changes a member's `MembershipLevel` in the UI?

- **Option A – Auto-recalculate claims on level change:** The save action in `EditMember.razor` also updates Identity claims to match the new level; the member's session is invalidated (or they see changes on next login).
- **Option B – Manual claim management only:** No automatic recalculation; admin must manually remove claims via a separate UI action. Simpler to implement, but creates a window where stale claims grant excess access.

**Your answer:**
<!-- e.g. "Option A – auto-recalculate claims on level change" -->
