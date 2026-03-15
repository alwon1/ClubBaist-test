# UC-TT-04 – Manage Standing Tee Time Requests (Deferred)

## Goal / Brief Description
Allow members/admin to create and maintain recurring standing tee-time requests that can influence future slot availability.

## Primary Actor
- Member

## Supporting Actors
- Admin/Clerk

## Trigger
- Actor submits or updates a standing tee-time request.

## Preconditions
1. Member is active.
2. Standing-request window/policy allows request submission.

## Postconditions
### Success
1. Standing request is stored/updated.
2. Request status is visible for review/assignment.
3. Resulting allocations (when assigned) feed tee-time availability.
4. Changes are auditable for request and assignment decisions.

### Failure
1. Request is not changed.
2. Validation/policy conflict is returned.

## Business Notes
1. Standing requests are intentionally separate from one-off reservation flows.
2. Availability integration is required, but assignment logic is its own domain behavior.