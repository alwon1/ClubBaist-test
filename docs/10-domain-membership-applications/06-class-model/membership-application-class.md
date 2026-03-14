# MembershipApplication (Domain Class)

## Purpose
Represents a single membership application and its lifecycle from submission through committee decision.

## Responsibilities
- Store application identity and submission info.
- Hold current application status.
- Link to applicant identity via ASP.NET Identity (`ApplicationUser`).
- Store all required application form fields directly on the application.
- Support status transitions (`Submitted / OnHold / Waitlisted / Accepted / Denied`).
- Trigger account-creation workflow when moved to `Accepted` (via service/transaction workflow).

## Core Properties
- `ApplicationId` (Guid/int)
- `ApplicationUserId` (generic key type `TKey`)
- `ApplicationUser` (`ApplicationUser<TKey>` navigation)
- `CurrentStatus` (`ApplicationStatus`)
- `SubmittedAt` (`DateTime`)
- `LastStatusChangedAt` (`DateTime`)
- `FirstName`
- `LastName`
- `Occupation`
- `CompanyName`
- `Address`
- `PostalCode`
- `Phone`
- `AlternatePhone` (nullable)
- `Email`
- `DateOfBirth`
- `RequestedMembershipCategory` (`Shareholder | Associate`)
- `Sponsor1MemberId` (`TKey`)
- `Sponsor2MemberId` (`TKey`)

## Optional (if needed now)
- None currently.

## Invariants (rules that must always be true)
- `ApplicationUserId` is required.
- Initial status must be `Submitted`.
- All required form fields must be present before submit.
- `Sponsor1MemberId` and `Sponsor2MemberId` are required.
- Every status change appends an `ApplicationStatusHistory` record.

## Explicit Non-Rule (current design)
- `MemberAccount` existence is not globally constrained by application status, because legacy members may predate the application system.

## Allowed Status Transitions (v1)
- `Submitted -> OnHold | Waitlisted | Accepted | Denied`
- `OnHold -> OnHold | Waitlisted | Accepted | Denied`
- `Waitlisted -> Waitlisted | OnHold | Accepted | Denied`
- `Accepted -> (terminal in v1)`
- `Denied -> (terminal in v1)`

## Key Methods (conceptual)
- `Submit(...)`
- `ChangeStatus(newStatus, changedBy, changedAt)`
- `CanTransitionTo(newStatus)`

(No `MarkAccountCreated()` method — account creation is enforced by transaction/workflow, not a tracked flag on this entity.)
