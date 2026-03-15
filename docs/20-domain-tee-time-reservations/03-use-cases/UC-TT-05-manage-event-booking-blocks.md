# UC-TT-05 – Manage Event Booking Blocks (Deferred)

## Goal / Brief Description
Allow authorized staff to reserve or block tee-sheet capacity for tournaments/special events so member booking availability reflects event constraints.

## Primary Actor
- Admin/Event Staff

## Trigger
- Staff creates, updates, or removes an event booking block.

## Preconditions
1. Actor has event-scheduling authorization.
2. Event date/time range is provided.

## Postconditions
### Success
1. Event block is stored/updated.
2. Affected tee-time slots show reduced/zero member capacity.
3. Changes are auditable.

### Failure
1. Event block is not changed.
2. Validation/conflict error is returned.

## Business Notes
1. Event blocks must be reflected by the same availability calculation used for normal bookings.
2. Event workflows are planned as a separate lifecycle from standard reservation CRUD.