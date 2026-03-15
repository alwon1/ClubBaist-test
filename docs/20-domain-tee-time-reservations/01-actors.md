# Tee Time Reservations – Actors

## Primary Actors
- Member (Gold, Silver, Bronze)
- Admin/Clerk (books and manages reservations on behalf of members)

## Supporting Actors
- Membership/Identity Service (member status and membership type checks)
- Reservation Service (availability, capacity, and reservation lifecycle)

## Actor Notes
1. Time-of-day access restrictions are evaluated using the membership type of the booking member (the member account being booked), not the acting user.
2. Members can book for themselves and include additional individual players in the same reservation.
3. Unrelated reservations may share the same tee-time slot as long as total slot capacity is not exceeded.
4. For staff-assisted bookings, authorization and audit attribution are based on the acting user (Admin/Clerk), while policy enforcement (eligibility/time-window) remains based on the booking member.
