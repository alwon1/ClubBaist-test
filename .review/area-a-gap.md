# Area A: Membership Applications — Gap Analysis

## Summary

The membership application workflow has a solid structural foundation: the application form captures most required fields, the status state machine covers all four required outcomes (accepted, denied, on-hold, waitlisted), and approval correctly creates a user account and membership record. However, the implementation is missing almost every business rule that guards the application process itself — sponsor validation (shareholder-only, 5-year tenure, annual sponsorship limit), fee and pricing data, age-based membership eligibility, and signature/date capture are all absent. The `MembershipLevel` entity carries no fee, category, or tier information, meaning the rich tier structure defined in the spec exists only as free-text names in the database.

---

## Missing Features

1. **Membership tier / pricing data** — The spec defines four tiers (Gold, Silver, Bronze, Copper) with specific sub-types and annual fees per sub-type. `MembershipLevel` has only `Name` and `ShortCode`; there is no `AnnualFee`, `Tier`, or `Category` field anywhere. Severity: **High**

2. **Entrance fee and share purchase tracking** — The spec requires a $10,000 entrance fee (payable over two years in four $2,500 instalments) and a $1,000 share purchase price. No entity, service, or UI exists for any of this. Severity: **High**

3. **Annual fees and billing cycle** — Yearly fees due April 1, the 10% late-payment penalty, and the $500/year minimum food-and-beverage charge for Gold-tier members are not modelled anywhere. Severity: **High**

4. **Signature and date capture** — The spec's application form requires an applicant signature and date, plus a separate shareholder name (print), signature, and date from each of the two sponsors. None of these fields exist on `MembershipApplication` or in the `Apply.razor` form. Severity: **High**

5. **Sponsor name (print) field** — The spec requires a printed shareholder name for each sponsor section; the application stores only an integer member ID. Severity: **Medium**

6. **Account creation notification / password setup** — When an application is approved, `ApproveMembershipApplicationAsync` creates the account with the hard-coded temporary password `"ChangeMe123!"` and sets `City = "Unknown"`, `Province = "Unknown"`. There is no mechanism to notify the new member or prompt them to set their own password. Severity: **Medium**

7. **Privacy consent field** — The spec states that information may not be used for purposes other than the membership roster without written permission. No privacy-consent checkbox or field is captured in the application. Severity: **Low**

---

## Incorrect Implementations

1. **Sponsor validation is existence-only, not shareholder-only** — The spec requires both sponsors to be existing *Shareholder* members. `Apply.razor` checks only that `m.Id == _model.Sponsor1MemberId` exists in the `MemberShips` table (`AnyAsync(m => m.Id == ...)`). It does not verify that either sponsor holds a Shareholder-level membership. Any member of any level can be recorded as a sponsor. Spec: "must be sponsored by TWO existing SHAREHOLDER members."

2. **Valid status transitions are overly permissive** — In `ReviewApplication.razor`, `_validTransitions` is built as every `ApplicationStatus` value except the current one, including transitioning from `Denied` back to `Submitted` or from `Accepted` to `OnHold`. The service layer blocks `SetApplicationStatusAsync` from setting terminal statuses, but the UI presents those options as selectable. A reviewer can select "Accepted" from the dropdown and `HandleDecisionAsync` routes it to `ApproveMembershipApplicationAsync`, but the UI also shows options like "Submitted" or "Waitlisted" for already-accepted applications where the card should show no transitions. The guard at the domain service level prevents the terminal path, but the UI is misleading.

3. **`MemberShipInfo.Id` range constraint is inconsistent with sponsor lookup** — `MemberShipInfo.Id` has `[Range(1000, int.MaxValue)]` enforced at the model level, yet the default value for `Sponsor1MemberId` and `Sponsor2MemberId` on `ApplicationFormModel` in `Apply.razor` is `null` (nullable `int?`). However, the validation requires a value, so a user entering any ID below 1000 would pass UI validation but fail the existence check silently — there is no informative error message that IDs must be 1000 or higher.

4. **Duplicate application check is email-only, not identity-based** — `SubmitMembershipApplicationAsync` blocks a second application for the same email address, but nothing prevents the same person from submitting with a different email. For dependents (Pee Wee, Junior, Intermediate), the applicant may share an address but use a different email. Date of birth is stored but not used to detect potential duplicates.

5. **`Apply.razor` ignores the return value of `SubmitMembershipApplicationAsync`** — At line 258 the result of `await ApplicationService.SubmitMembershipApplicationAsync(application)` is discarded. If submission fails (duplicate email or the applicant is already a member), `_submitted` is still set to `true` and the success message is shown, misleading the user.

---

## Business Rules Not Enforced

1. **Sponsor must have been a member for at least 5 years** — No membership start-date field exists on `MemberShipInfo`, and no tenure check is performed anywhere in the application or service layer.

2. **Sponsor must be in good standing** — "Good standing" is never defined in the data model (no `IsInGoodStanding`, no overdue-balance flag), and is not checked during submission.

3. **Each sponsor may sponsor at most 2 prospective members per year** — No query or counter checks how many active (non-denied) applications a given sponsor ID already appears on within the current calendar year.

4. **Sponsors must be different from each other** — The UI in `Apply.razor` does check `Sponsor1MemberId == Sponsor2MemberId` and rejects that case. This rule is enforced in the UI but not in `MembershipApplicationService.SubmitMembershipApplicationAsync`, so it can be bypassed by calling the service directly.

5. **Age eligibility for dependent membership levels** — Bronze sub-types have strict age ranges (Pee Wee: 6–11, Junior: 12–17, Intermediate: 18–24). `DateOfBirth` is captured but never validated against the requested membership level. An 8-year-old could apply for a Gold Shareholder membership without any rejection.

6. **Membership committee reviews once per month** — There is no scheduling, batching, or lock mechanism to enforce or surface a monthly review cadence. Applications are individually actionable at any time.

7. **10% penalty on overdue balances** — Not implemented; there is no billing or balance model at all.

8. **$500/year minimum food and beverage for Gold members** — Not implemented; no food-and-beverage tracking exists.

9. **Yearly fees due April 1** — No annual renewal, fee generation, or due-date logic exists.

---

## Edge Cases Not Handled

1. **Sponsor loses shareholder status after application is submitted but before review** — Because sponsor eligibility is only checked (incompletely) at submission time, a sponsor who is downgraded between submission and committee review is not flagged. The review UI shows only the sponsor's current name and membership number with no eligibility re-check.

2. **Sponsor becomes a member of fewer than 5 years before review** — Not applicable as a regression, but initial tenure is never recorded, so the check can never be made at any point.

3. **Applicant is already a member under a different email** — `SubmitMembershipApplicationAsync` checks `db.MemberShips.AnyAsync(m => m.User.Email == application.Email)` (email match only). An existing member using a new email address can submit a fresh application undetected.

4. **Approval with wrong membership level** — The review UI lets the committee select any membership level from the full list when approving, regardless of what the applicant requested. The applicant's `RequestedMembershipLevelId` is ignored during approval; whatever the reviewer picks in the dropdown is applied.

5. **Concurrent approval of the same application** — `ApproveMembershipApplicationAsync` uses Snapshot isolation, which protects against dirty reads but does not prevent two concurrent approvals from both succeeding if they execute the `FindAsync` before either commits. A unique constraint on the user email in the identity table will ultimately prevent the second user creation, but the application's `Status` may be set to `Accepted` twice without error in certain timing windows.

6. **Application submitted for a membership level that does not exist** — The `Apply.razor` form loads levels from the database and populates a dropdown, so this is partially guarded in the UI, but the `MembershipApplication` entity itself has no FK enforcement note preventing a stale `RequestedMembershipLevelId` (e.g., if the level is deleted between page load and form submit).

7. **Sponsor ID 0 accepted as valid** — `ApplicationFormModel.Sponsor1MemberId` defaults to `null`, but if a user somehow submits `0`, the `AnyAsync(m => m.Id == 0)` check will return false and give a "not found" message rather than a validation error, because `0` is a valid `int` that passes `[Required]` on a nullable int in Blazor (Blazor treats non-null as satisfying Required for value types bound through nullable).

---

## UI Issues

1. **Sponsor fields collect member IDs, not names** — The spec requires "Shareholder Name (print), Signature, Date" for each sponsor. The form presents numeric member ID inputs with no name lookup or autocomplete. A prospective applicant cannot be expected to know their sponsor's internal database ID.

2. **Missing fields on the application form** — The spec lists these required fields that are absent from `Apply.razor`: applicant Signature, applicant Date (of signing), and both sponsor sections (Shareholder Name print, Signature, Date).

3. **No Occupation or Company Name validation beyond Required/MaxLength** — The spec does not restrict these further, but the form provides no hint text or format guidance, unlike the postal code and phone fields.

4. **ApplicationInbox does not show submission date** — Committee members have no way to see when an application was submitted; `MembershipApplication` has no `SubmittedAt` timestamp, and the inbox table omits even the applicant date-of-birth or any time-ordering cue beyond the auto-increment `Id`.

5. **ReviewApplication shows "Submitted" as a valid transition for OnHold/Waitlisted applications** — The `_validTransitions` list includes `ApplicationStatus.Submitted` as an option when the current status is `OnHold` or `Waitlisted`, which has no business meaning and will succeed via `SetApplicationStatusAsync`.

6. **No confirmation step before irreversible decisions** — Clicking "Submit Decision" for Accepted or Denied immediately calls the service with no confirmation dialog. Denials are logged but the application UI provides no undo or comment field to record a reason.

7. **ReviewApplication sponsor display shows only member ID when sponsor lookup fails** — `GetSponsorName` falls back to `"Member #{sponsorId}"` if the ID is not found in the loaded sponsor dictionary. This gives the reviewer no useful information for validating sponsor eligibility.

---

## Test Coverage Gaps

1. **No test for sponsor shareholder validation** — Because the rule is not implemented, there is also no test asserting that a non-shareholder sponsor ID is rejected.

2. **No test for sponsor 5-year tenure rule** — No test exists for any tenure-related check.

3. **No test for sponsor annual limit (2 per year)** — No test covers the scenario where a sponsor has already co-signed two active applications.

4. **No test for age-based membership eligibility** — No test verifies that an applicant's date of birth is compatible with the requested level (e.g., a 30-year-old applying for Pee Wee should be rejected).

5. **No test for the duplicate-sponsor check at the service layer** — The `Sponsor1MemberId == Sponsor2MemberId` guard lives only in the Blazor page code-behind. There is no service-layer test asserting that duplicate sponsors are rejected.

6. **No test for the ignored return value bug in `Apply.razor`** — The silent discard of `SubmitMembershipApplicationAsync`'s return value is not covered by any test; it would require a UI/integration test.

7. **No test for concurrent approval** — There is no concurrency test for `ApproveMembershipApplicationAsync`.

8. **No test for `DenyApplicationAsync`** — Only `ApproveMembershipApplicationAsync`, `SetApplicationStatusAsync`, and `SubmitMembershipApplicationAsync` are directly exercised in `ServiceBehaviorTests.cs`. `DenyApplicationAsync` has no dedicated test.

9. **No test for already-a-member submission block** — `SubmitMembershipApplicationAsync` checks `db.MemberShips.AnyAsync(m => m.User.Email == application.Email)` and returns `false`, but there is no test that exercises this path.

10. **No test for approval of a non-existent application** — The guard at lines 47–53 of `MembershipApplicationService.cs` returns `false` for a missing application, but this path is untested.

---

## Notes

- `MemberShipInfo` (the accepted-member record) lives in the `ClubBaist.Domain2` root namespace while `MembershipApplication` lives in `ClubBaist.Domain2.Entities.Membership`. This namespace inconsistency may cause confusion when navigating the codebase.
- The temporary password `"ChangeMe123!"` is hard-coded in `ApproveMembershipApplicationAsync`. This is a security concern: if the email notification step is never built, new members will have a known, shared default password with no forced-reset mechanism.
- `City` and `Province` are hard-coded to `"Unknown"` when a member account is created from an approved application, because the application form does not collect these fields. This leaves member profile data permanently incomplete unless manually corrected.
- The `MembershipLevel` entity has no `Fee`, `Tier`, or `IsGolfPrivilege` property, meaning the four-tier structure (Gold/Silver/Bronze/Copper) and all associated pricing must be maintained externally (e.g., seeded as named records). There is no seeding script or migration visible in the reviewed paths that initialises the required levels.
- The `Apply.razor` page has no authentication requirement (`@attribute [Authorize]` is absent), meaning anyone — including unauthenticated visitors — can submit a membership application. Whether this is intentional (open application process) or an oversight is not stated in the spec, but it creates an obvious spam/abuse vector.
