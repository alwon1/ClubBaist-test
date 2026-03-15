# UC-TT-02 – Review and Maintain Reservation

## Goal / Brief Description
Allow members and authorized staff to view reservation details and maintain reservations (update or cancel) while preserving shared slot-capacity consistency.

## Primary Actor
- Member

## Supporting Actors
- Admin/Clerk
- Reservation Service
- Membership/Identity Service

## Trigger
- Actor opens existing reservation(s) to review, update, or cancel.

## Preconditions
1. Reservation exists.
2. Actor is authorized (owner member or authorized staff).
3. For updates, new date/time/player data is provided.
4. For updates, revised reservation remains within season and eligibility rules.

## Postconditions
### Success
1. Reservation details are returned (view).
2. Reservation is updated or canceled as requested.
3. Slot occupancy is adjusted accurately (decrement old slot/increment new slot as needed).
4. No audit trail requirement is defined for routine update/cancel actions in current scope.

### Failure / Partial
1. Reservation remains unchanged.
2. Validation, authorization, or capacity conflict error is returned.

## Main Success Flow (Update)
1. Actor opens reservation detail.
2. System shows current reservation, players, and slot occupancy context.
3. Actor submits requested update (date/time and/or player list).
4. System validates authorization and member-eligibility rules.
5. System validates participant-list rules (booking member remains on reservation; only additional players may be added/removed).
6. System validates revised slot capacity.
7. System applies update atomically and adjusts occupancy.
8. System confirms update.

## Alternate Flows
### A1 – Cancel Reservation
- At step 3, actor chooses cancel.
- System validates reservation is active and actor is authorized (owner member or authorized staff).
- System marks reservation canceled and releases slot occupancy.
- System confirms cancellation.

### A2 – Revised Booking Exceeds Capacity
- At step 5, revised players exceed remaining capacity at the target slot.
- System rejects update and returns current availability.

### A3 – Unauthorized Access
- At step 4, actor is not owner and lacks required staff role.
- System denies operation and logs authorization failure.

### A4 – Membership Time Restriction on Update
- At step 4, revised time is invalid for booking member's membership type.
- System rejects update and returns allowed windows.

### A5 – Booking Member Removal Attempted
- At step 5, update removes the booking member from the reservation player list.
- System rejects update with a policy reason (for example, `BOOKING_MEMBER_REQUIRED`).
- System instructs actor to cancel the reservation instead if the booking member will not play.

## Exceptions
- **E1: Concurrency Conflict**: Reservation or capacity changed by another request; system prompts refresh and retry.
- **E2: Transaction Error During Move**: Failure while moving between slots causes rollback; occupancy remains consistent.
- **E3: Invalid Cancellation State**: Reservation is already canceled/not active; system rejects cancel request.
- **E4: Cancellation Authorization Failure**: Actor is not allowed to cancel this reservation.

## Related Business Rules / Notes
1. Shared slot capacity never exceeds four total players.
2. Update and cancel operations must preserve capacity integrity.
3. Members can only maintain their own reservations; staff roles may maintain reservations on behalf of members.
4. Player identities remain visible in reservation details.
5. Update operations are participant-list based: additional players may be added/removed, but the booking member cannot be removed from an active reservation.
6. If the booking member will not participate, the valid operation is cancellation (with rebooking if needed), not participant removal.
7. No explicit cutoff policy for updates/cancellations is defined yet; operations are allowed unless future policy adds limits.
8. Phase 1 defines no cancellation cutoff; cancellation timing is not a rejection criterion.
9. Cancellation reason/decision codes align with policy service and do not include cutoff-based rejection in Phase 1.

## Outcome / Reason Codes (Phase 1 alignment)
- `BOOKING_ALLOWED` – update/cancel action passed current rules.
- `BOOKING_FORBIDDEN` – actor lacks permission to maintain reservation.
- `BOOKING_NOT_FOUND_OR_NOT_ACTIVE` – reservation missing or not in active state.
- `BOOKING_WINDOW_VIOLATION` – update target date is outside allowed booking window.
- `PLAYER_COUNT_OUT_OF_RANGE` – revised player count violates min/max policy.

> `CANCELLATION_CUTOFF_EXCEEDED` is deferred to a future phase and is not emitted in UC-TT-02 flows.

## Initial SSD (System Sequence Diagram)

```mermaid
sequenceDiagram
  actor Actor as Member/Admin
  participant System as Reservation System

  Actor->>System: Open reservation
  System-->>Actor: Show reservation + players + occupancy
  Actor->>System: Submit update/cancel
  System->>System: Validate auth + eligibility + capacity
  alt Update valid
    System->>System: Apply atomic reservation update
    System-->>Actor: Update confirmation
  else Cancel
    System->>System: Mark canceled + release capacity
    System-->>Actor: Cancel confirmation
  else Invalid
    System-->>Actor: Error + current availability
  end
```
