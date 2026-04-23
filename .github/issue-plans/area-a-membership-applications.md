# Area A: Membership Applications – Design Questions

> **Answer after Areas F and E.**

These decisions shape the membership application workflow: `Apply`, `ApplicationInbox`, and `ReviewApplication`.

**Related review files:** `.review/area-a-gap.md`, `.review/area-a-design.md`, `.review/area-a-ui.md`, `.review/area-a-priority.md`

---

## Question 9 – Sponsor lookup UX

Currently the `Apply.razor` form requires applicants to enter their sponsor's **numeric member ID** manually.

Should applicants be able to **search for sponsors by name** (requires a new lookup endpoint), or do we **keep the numeric member-ID entry but add an inline name-display** once an ID is typed?

- **Option A – Search by name:** Applicants type a name and see a list of matching members to choose from. Requires a new server-side search endpoint (e.g., `/api/members/search?q=...`). More user-friendly but more implementation effort.
- **Option B – Keep numeric ID + inline name display:** Once an applicant types a member ID, the member's name is fetched and shown inline for confirmation. Smaller change; still requires a backend call per ID.
- **Option C – Keep numeric ID only, no name display:** Applicants must know the sponsor's ID externally. No backend changes needed; worst UX.

**Your answer:**
<!-- e.g. "Option B – keep numeric ID but add inline name confirmation" -->

---

## Question 10 – Approval notification flow

When a membership application is **approved**, what should happen?

Currently: a new `ClubBaistUser` + `MemberShipInfo` is created with a **hard-coded default password `"ChangeMe123!"`** (issue #99 — this is a known security concern).

What is the desired confirmation/notification flow for the approved applicant?

- **Option A – Auto-send email with a password-reset link:** The system sends the new member an email with a one-time link to set their password. Requires an email service (SMTP config or provider).
- **Option B – Display a one-time generated password to the admin:** A random strong password is generated, shown once to the admin who approves, and the admin communicates it to the new member out-of-band.
- **Option C – Create account with no password + force reset at first login:** The account is created in a "must reset password" state; the member must go through a forgot-password flow to activate.
- **Option D – Defer email/notification entirely:** Fix the hard-coded default password (replace with a random one-time password) but do not build notification infrastructure in this phase.

**Your answer:**
<!-- e.g. "Option B – display one-time password to admin (fixes #99, defers email)" -->

---

## Question 11 – MembershipLevel entity enrichment

Should the `MembershipLevel` entity be **enriched now** with additional fields, or are these out of scope for this phase?

Proposed additions:
- `AnnualFee` (decimal) — membership fee for finance tracking
- `MemberType` (enum: `Shareholder` vs `Associate`) — formalises the current SH/AS distinction
- `MinimumAge` / `MaximumAge` (int?) — age eligibility enforcement during application review

- **Option A – Add all fields now:** Enables fee tracking and age validation in `ReviewApplication.razor`; requires a migration.
- **Option B – Add `MemberType` only:** Formalises the SH/AS distinction without fee tracking; small migration.
- **Option C – Defer all enrichment:** `MembershipLevel` stays as-is (`Id`, `Name`, `ShortCode`); fee tracking and age enforcement are out of scope for this phase.

**Your answer:**
<!-- e.g. "Option C – defer all enrichment; fee tracking is out of scope" -->
