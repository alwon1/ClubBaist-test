# Tee Time Reservations – Actors

## Primary Actors
- Member (Gold, Silver, Bronze)
- Admin/Clerk (books and manages reservations on behalf of members)

## Supporting Actors
- Membership/Identity Service (member status and membership type checks)
- Reservation Service (availability, capacity, and reservation lifecycle)

## Actor Notes
1. Time-of-day access restrictions are evaluated using the membership type of the member performing the booking.
2. Members can book for themselves and include additional individual players in the same reservation.
3. Unrelated reservations may share the same tee-time slot as long as total slot capacity is not exceeded.
