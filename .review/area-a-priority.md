# Area A: Membership Applications — Prioritized Task List

## Scoring Key
- **Impact:** H = high, M = medium, L = low (relative to business value and correctness)
- **Effort:** XS = 1–2h, S = half-day, M = 1–2 days, L = 3–5 days, XL = week+
- **Priority Tiers:** Critical (bugs / data loss / security) → High (spec gaps) → Medium (design / maintainability) → Low (polish / full Fluent UI rewrite)

---

## Critical

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| C1 | Fix `Apply.razor` discarding the return value of `SubmitMembershipApplicationAsync` — currently shows success message even when submission fails (duplicate email, already a member) | H | XS | One-line check-and-branch after `await`. The gap and UI analyses both flag this as a confirmed silent failure. |
| C2 | Add `[Authorize]` to `Apply.razor` — page is currently publicly accessible with no authentication gate, creating a spam/abuse vector | H | XS | Decide whether the application process is intentionally open. If unauthenticated guests may apply, add anti-spam controls (CAPTCHA, rate limiting) and document the decision. |
| C3 | Fix sponsor validation to require Shareholder status, not just membership existence — `Apply.razor` and `MembershipApplicationService` both check `AnyAsync(m => m.Id == ...)` without verifying the sponsor's membership level | H | S | Requires a join to `MembershipLevel` by `ShortCode == "SH"` (or by a `MemberType` enum once H1 lands). Service-layer check is the trust boundary; UI check is secondary. |

---

## High

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| H1 | Move duplicate-sponsor check from UI into `MembershipApplicationService.SubmitMembershipApplicationAsync` — currently enforced only in `Apply.razor` code-behind, bypassable by calling the service directly | H | XS | Three-line guard at the top of the service method. |
| H2 | Replace hard-coded `"ChangeMe123!"` initial password in `ApproveMembershipApplicationAsync` with a configuration option (`IOptions<MembershipApprovalOptions>`) and add a forced-reset-on-first-login mechanism | H | S | Security issue. Until a notification system exists, at minimum move the value to `appsettings.json` and document the risk. |
| H3 | Change `SubmitMembershipApplicationAsync` return type from `bool` to `(bool Success, string? Reason)` (or a small `ApplicationResult` record) so callers can surface the correct error message | M | S | Enables C1 fix to show meaningful failure feedback. Affects `Apply.razor` call site and any tests. |
| H4 | Add `SubmittedAt` (`DateTimeOffset`, set by service on insert) to `MembershipApplication` — currently there is no timestamp; the ApplicationInbox has no way to order by age | M | XS | One nullable column + migration. Set in `SubmitMembershipApplicationAsync` before `SaveChangesAsync`. Also add the column to the ApplicationInbox grid. |
| H5 | Change `MembershipApplication.DateOfBirth` from `DateTime` to `DateOnly` — `DateTime` risks timezone-shifted values under EF | M | XS | Column type change from `datetime2` to `date` in migration. Align `ClubBaistUser.DateOfBirth` at the same time. |
| H6 | Fix `ReviewApplication.razor` `_validTransitions` — replace "any status except current" logic with an explicit allowed-transition map to prevent nonsensical transitions (e.g., `Waitlisted → Submitted`) | M | XS | Map: `Submitted → {OnHold, Waitlisted, Accepted, Denied}`, `OnHold → {Submitted, Waitlisted, Accepted, Denied}`, `Waitlisted → {OnHold, Accepted, Denied}`. Terminal states show no transitions. |
| H7 | Add a `<FluentDialog>` (or Bootstrap modal) confirmation before irreversible decisions (`Accepted`, `Denied`) in `ReviewApplication.razor` — currently a mis-click submits immediately with no undo | M | S | Can be done with existing Bootstrap modal before full Fluent UI rewrite. |

---

## Medium

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| M1 | Add `AnnualFee` (`decimal`), `MemberType` (`enum: Shareholder, Associate`), `MaxCapacity` (`int?`), and `IsActive` (`bool`) to `MembershipLevel` — currently the entity has only `Name` and `ShortCode`, making the tier structure data-free | M | M | Single migration. Populate via updated seed or admin UI. `MemberType` discriminator unblocks C3 (Shareholder sponsor check). |
| M2 | Add `City` and `Province` string fields to `MembershipApplication` — currently the approval method hard-codes `City = "Unknown"`, `Province = "Unknown"` when creating the member account | M | S | Migration + form fields in Apply.razor + copy in `ApproveMembershipApplicationAsync`. Remove the `"Unknown"` hard-code. |
| M3 | Fix `MembershipService.SetMembershipLevelForUserAsync` — check `SaveChangesAsync()` return value before returning `true`; add a transaction wrapping the read + write | M | XS | Currently returns `true` even if no rows were saved. |
| M4 | Remove unnecessary snapshot transaction from `MembershipLevelService.CreateMembershipLevelAsync` — a single-entity insert is already atomic | L | XS | Remove `ExecutionStrategy` + `BeginTransactionAsync` from the create method only. |
| M5 | Rename `MemberShipInfo` → `MembershipInfo` and `db.MemberShips` → `db.Memberships` — fix capitalisation to match C# naming conventions | L | S | Pure rename. Rename class, DbSet, namespace, and all 38+ references. Do as a single atomic PR for readable diffs. Coordinate with the Area F "2" suffix rename. |
| M6 | Add age-eligibility validation for dependent membership levels — `DateOfBirth` is captured but never validated against the requested level (e.g., Pee Wee: 6–11, Junior: 12–17) | M | M | Requires age-range data on `MembershipLevel` (`MinimumAge`, `MaximumAge` int? fields) and a service-layer check in `SubmitMembershipApplicationAsync`. |
| M7 | ApplicationInbox UX fixes: add submission date column (once H4 lands), make status filter reactive on change (remove manual Search button), add applicant age column, default sort by "Days Waiting" | M | S | CSS-only / Blazor data binding changes; no migration needed once H4 is done. |
| M8 | Add a `Notes` / `Reason` field to the decision panel in `ReviewApplication.razor` — required for Denied decisions, optional otherwise. Persist with the application record | M | M | Adds `DecisionNotes` string column to `MembershipApplication` + migration. Required field guard in HandleDecisionAsync for Denied. |

---

## Low

| # | Task | Impact | Effort | Notes |
|---|------|--------|--------|-------|
| L1 | Add sponsor 5-year tenure check — requires adding `MembershipStartDate` (`DateOnly?`) to `MembershipInfo` + service-level check in `SubmitMembershipApplicationAsync` | M | M | `MembershipStartDate` is set when an application is approved. Migration + seed back-fill for existing members. |
| L2 | Add sponsor annual-limit enforcement — at most 2 active (non-Denied) applications per sponsor per calendar year | M | M | One additional `AnyAsync` count query in `SubmitMembershipApplicationAsync`. |
| L3 | Make `Sponsor1MemberId` / `Sponsor2MemberId` on `MembershipApplication` nullable FK properties with proper navigation — currently plain `int` with no EF referential integrity | L | S | Migration: allow null, add FK constraint. Needed before L1 can eager-load sponsor membership records. |
| L4 | Full Fluent UI redesign — `Apply.razor` as `<FluentWizard>` (3 steps: Personal, Employment, Membership & Sponsors) with `<FluentAutocomplete>` for sponsor lookup, `<FluentDatePicker>` with age-floor `Max`, `<FluentCheckbox>` for consent | M | L | Design reference in area-a-ui.md. Requires Fluent UI package install (Area F L-tier work). |
| L5 | Full Fluent UI redesign — `ApplicationInbox.razor` as `<FluentDataGrid>` with column sort, reactive filter, search, `<FluentBadge>` status, `<FluentPaginator>` | M | M | Design reference in area-a-ui.md. |
| L6 | Full Fluent UI redesign — `ReviewApplication.razor` with sticky decision panel, `<FluentPersona>` sponsor cards, `<FluentDialog>` confirmation, `<FluentTextArea>` notes | M | L | Design reference in area-a-ui.md. Depends on M8 (notes field). |
| L7 | Add `ApplicantSignature` (typed-name attestation string) and `SignedDate` (`DateOnly`) to `MembershipApplication` + consent checkbox in Apply form | L | M | Legal/compliance item. Low urgency until the club's legal requirements are confirmed. |
| L8 | Billing model — annual fees, entrance fee instalment tracking, 10% late-payment penalty, $500 F&B minimum for Gold | H | XL | Requires a new `MemberFeeRecord` or `Invoice` entity, billing cycle service, and admin UI. Not scoped as part of this review. Flag for a separate project. |

---

## Grouped Tasks (must ship together)

| Group | Tasks | Reason |
|-------|-------|--------|
| Submit correctness | C1 + H3 | H3 (result type change) enables C1 (surface the error). Same files (`Apply.razor` + service). |
| Data model additions | H4 + H5 + M2 | All add columns to `MembershipApplication`; combine into one migration. |
| Sponsor FK hardening | L3 + C3 + L1 | L3 (FK navigation) is prerequisite for L1 (tenure check); C3 (Shareholder check) benefits from the navigation too. |
| Level enrichment | M1 + M6 | `MinimumAge`/`MaximumAge` fields for M6 go on the `MembershipLevel` entity being changed in M1; one migration. |

## Independent Tasks (can be parallelised)

C2, H1, H2, H6, H7, M3, M4, M5 (rename), M7, M8, L2, L4, L5, L6, L7
