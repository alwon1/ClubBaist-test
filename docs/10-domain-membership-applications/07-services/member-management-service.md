# MemberManagementService (Domain/Application Service)

## Purpose
Provide member account management operations for membership administration workflows.

## Current Scope (v1 - bare minimum)
- Create a new `MemberAccount` record.

## Responsibilities
- Accept validated input for new member creation.
- Create and persist a `MemberAccount` linked to `ApplicationUser`.
- Return a created-member result (identifier and summary fields).

## Core Operation (v1)
- `CreateMemberAsync(createMemberRequest, createdByUserId, cancellationToken)`

### Inputs
- `ApplicationUserId` (required)
- Member profile fields required by `MemberAccount`
- Initial membership category
- Optional initialization metadata (e.g., active flag defaults)

### Outputs
- `MemberAccountId`
- `MemberNumber`
- `CreatedAt`

## Invariants / Rules (v1)
- `ApplicationUserId` is required.
- `MemberNumber` must be unique.
- Required `MemberAccount` fields must be present.
- Creation must fail if linked user does not exist.

## Deferred / Future (currently unplanned)
- Update member profile details.
- Change membership category.
- Activate/deactivate member accounts.
- Soft-delete/remove member accounts.
- Additional policy checks and audit hooks.

## Explicit Non-Rule (current design)
- This service does not currently handle membership-application decision logic; it only creates `MemberAccount` records when called.

## Suggested Dependencies
- `IApplicationUserRepository` (or Identity user manager abstraction)

## Mermaid Service Context

```mermaid
flowchart LR
  Caller[Membership Workflow / Admin Action] --> MMS[MemberManagementService]
  MMS --> URepo[IApplicationUserRepository]
```
