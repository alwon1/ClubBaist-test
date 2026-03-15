# UC-TT-03 – Admin Adjust Season Window (Deferred)

## Goal / Brief Description
Allow authorized admin users to open, shorten, extend, or temporarily close the season window in response to weather or operational conditions.

## Primary Actor
- Admin/Operations Staff

## Trigger
- Admin submits a season-window adjustment request.

## Preconditions
1. Actor has season-management authorization.
2. Current season configuration exists.

## Postconditions
### Success
1. Season window/state is updated.
2. Availability and booking validation use the new season values.
3. Change is recorded with actor and timestamp.

### Failure
1. Season values remain unchanged.
2. Validation/authorization error is returned.

## Business Notes
1. This use case affects booking eligibility globally.
2. Existing reservations may require separate follow-up policy (out of scope for this version).
3. Keep this use case separate from reservation CRUD.