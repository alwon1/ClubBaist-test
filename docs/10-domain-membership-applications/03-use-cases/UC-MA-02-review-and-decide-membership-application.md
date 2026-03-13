# UC-MA-02 – Review and Decide Membership Application

## Goal / Brief Description
Enable the membership committee to review applications in actionable statuses (Submitted, On-Hold, Waitlisted) and record decisions (Accepted, Denied, On-Hold, Waitlisted), including downstream account-creation initiation when accepted.

## Primary Actor
- Membership Committee Member

## Supporting Actors
- Membership Admin/Clerk
- Member Account Service
- Finance Admin/Committee

## Trigger
- Committee initiates a review cycle (typically monthly) or opens an individual application in an actionable status (`Submitted`, `On-Hold`, `Waitlisted`).

## Preconditions
1. At least one application exists with status `Submitted`, `On-Hold`, or `Waitlisted`.
2. Committee member is authorized to review/decide.

## Postconditions
### Success
1. Final decision status is recorded (Accepted / Denied / On-Hold / Waitlisted).
2. Status history/audit trail is updated.
3. If accepted, member account creation is initiated.

### Failure / Partial
1. No final decision stored.
2. Application remains in previous status.

## Main Success Flow
1. Committee member opens review queue.
2. System displays applications in actionable statuses (`Submitted`, `On-Hold`, `Waitlisted`) and key details.
3. Committee member opens an application for detailed review.
4. Committee member records decision as `Accepted`.
5. System validates decision and permissions.
6. System stores decision and updates status history.
7. System initiates member account creation workflow.
8. System confirms successful completion.

## Alternate Flows
### A1 – Decision = Denied
- At step 4, committee selects `Denied`.
- System stores denied status.
- Flow ends without account creation.

### A2 – Decision = On-Hold
- At step 4, committee selects `On-Hold`.
- System stores on-hold status.
- Flow ends without account creation.

### A3 – Decision = Waitlisted
- At step 4, committee selects `Waitlisted`.
- System stores waitlisted status.
- Flow ends without account creation.

### A4 – Re-evaluate On-Hold or Waitlisted Application
- At step 2, committee filters to `On-Hold` or `Waitlisted` applications.
- Committee selects an application and changes status to `Accepted`, `Denied`, or keeps it in `On-Hold`/`Waitlisted`.
- System stores updated status in history.
- If changed to `Accepted`, account creation workflow is initiated.

## Exceptions
- **E1: Concurrency Conflict**: Another committee member updated application simultaneously; system prompts reload and re-evaluation.
- **E2: Account Provisioning Error (Accepted only)**: Decision remains accepted but account creation is flagged for retry/manual follow-up.

## Related Business Rules / Notes
1. Applications are reviewed by membership committee on a regular cycle (monthly operationally).
2. Permitted decision outcomes are accepted, denied, on-hold, waitlisted.
3. Accepted applications trigger creation of member account data for downstream finance/membership operations.
4. Decision and status changes must be auditable.
5. Applications in `On-Hold` and `Waitlisted` remain actionable and may be moved to any valid decision status by committee members.

## Initial SSD (System Sequence Diagram)

```mermaid
sequenceDiagram
  actor Committee as Membership Committee Member
  participant System as Membership Application System
  participant Account as Member Account Service

  Committee->>System: Open pending applications
  System-->>Committee: Display review queue
  Committee->>System: Open application + submit decision
  System->>System: Validate authorization and decision data
  alt Decision = Accepted
    System->>System: Save status and decision history
    System->>Account: Initiate member account creation
    Account-->>System: Account creation acknowledged
    System-->>Committee: Decision saved; account provisioning initiated
  else Decision = Denied/On-Hold/Waitlisted
    System->>System: Save status and decision history
    System-->>Committee: Decision saved
  end
```
