# ApplicationStatusHistory (Domain Class)

## Purpose
Represents one status transition event for a membership application.

## Responsibilities
- Store a single status-change record.
- Link the change to a specific `MembershipApplication`.
- Record `FromStatus` and `ToStatus`.
- Record who made the change and when.

## Core Properties
- `ApplicationStatusHistoryId` (Guid/int)
- `MembershipApplicationId` (Guid/int)
- `MembershipApplication` (navigation)
- `FromStatus` (`ApplicationStatus`)
- `ToStatus` (`ApplicationStatus`)
- `ChangedByUserId` (`TKey`)
- `ChangedByUser` (`ApplicationUser<TKey>` navigation)
- `ChangedAt` (`DateTime`)

## Optional (if needed now)
- None currently.

## Invariants (rules that must always be true)
- `MembershipApplicationId` is required.
- `ToStatus` is required.
- `ChangedByUserId` is required.
- `ChangedAt` is required.
- `FromStatus` and `ToStatus` should not be equal for a transition record.

## Deferred / Future (currently unplanned)
- Enforcing creation of a history record for every status change globally.
- Sequence number/version per application for strict ordering.

## Explicit Non-Rule (current design)
- Not all status changes are guaranteed to be persisted in history in current scope (enforcement deferred).

## Allowed Status Transition Records (v1)
(Should mirror `MembershipApplication` transition rules)
- `Submitted -> OnHold | Waitlisted | Accepted | Denied`
- `OnHold -> OnHold | Waitlisted | Accepted | Denied`
- `Waitlisted -> Waitlisted | OnHold | Accepted | Denied`
- `Accepted -> (terminal in v1)`
- `Denied -> (terminal in v1)`

## Construction Pattern
Use constructor-based creation rather than a static factory method.

Example conceptual constructor:
- `ApplicationStatusHistory(membershipApplicationId, fromStatus, toStatus, changedByUserId, changedAt)`

## Extension Method Pattern (for ease of use)
Use an extension method on `MembershipApplication` to create history entries.

Example conceptual method:
- `membershipApplication.RecordStatusChange(newStatus, changedByUserId, changedAt)`

This extension can:
1. Read the current status from `membershipApplication`.
2. Build an `ApplicationStatusHistory` object via constructor.
3. Return the history record (or append to a collection when history enforcement is enabled).
