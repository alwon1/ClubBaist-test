# Standing Tee Times — Deferred

Standing tee time functionality is deferred from the initial implementation.

## What needs to be built

### Domain Model
- `StandingTeeTime` entity: recurring reservation for a specific day/time across the season
- Relationship to `MemberAccount` (booking member and player list)
- Season association

### Service Layer
- `StandingTeeTimeService`: CRUD for standing tee time requests
- Integration with `TeeTimeBookingService` to auto-generate individual `Reservation` records
- Conflict detection with existing reservations
- Admin approval workflow

### UI Pages
- Member: request a standing tee time (day of week, time, players)
- Admin: view/approve/deny standing tee time requests
- Admin: manage active standing tee times (cancel, modify)

### Booking Rules
- Standing tee time reservations should respect existing booking rules (capacity, membership restrictions, season)
- Priority handling when standing tee times conflict with one-off bookings
