# ClubBaist TODO

## Must Fix

- [ ] **No email uniqueness check on membership applications** — Submitting an application with an email that already has an active application (`sam.tester@example.com`) or that belongs to an existing member (`shareholder1@clubbaist.com`) both succeed silently with no error. Server-side uniqueness validation is missing entirely. (Failing: TC-MEM-004, TC-NEG-003)

- [ ] **IDOR vulnerability in ReservationDetail.razor** — Any authenticated member can view, edit players on, or cancel any reservation by navigating to `/teetimes/reservation/{any-guid}`. `LoadData()`, `SaveChanges()`, and `ConfirmCancel()` never verify the logged-in user owns the reservation. Non-admin members must be restricted to their own reservations.

- [ ] **Sponsor `[Required]` validation is a no-op in Apply.razor** — `Sponsor1MemberId` and `Sponsor2MemberId` are `Guid` (value type), not `Guid?`. `Guid.Empty` satisfies `[Required]`, so the form submits successfully with no sponsor selected. Fix: use `Guid?` or a custom validator rejecting `Guid.Empty`.

- [ ] **SeedWorker null-dereference on user creation failure** — `CreateUserWithRoleAsync` returns `null` on failure, but callers use the null-forgiving operator (`sh1!.Id`). If any user creation fails, this throws `NullReferenceException` instead of a meaningful error.

## Should Fix

- [ ] **Duplicate "Tee Times" nav link for Admin+Member users** — `NavMenu.razor` lines 23 and 38: an Admin who also has the Member role sees two "Tee Times" links, one from each `AuthorizeView` block.

- [ ] **Same sponsor selectable for both slots in Apply.razor** — No validation prevents selecting the same member as both Sponsor 1 and Sponsor 2.

- [ ] **Misleading test name in BookingRuleTests.cs** — `BookingWindowRule_DateOutsideSeason_ReturnsZero` asserts `-1`, not `0`. The rule return value was changed but the test name was not updated.

- [ ] **Exception messages leaked to users** — Every Razor page renders `ex.Message` directly in the UI (e.g., `$"Failed to load: {ex.Message}"`). Not XSS due to Blazor encoding, but database errors can leak connection strings, table names, etc. Display a generic message and log the exception server-side.

- [ ] **Hardcoded capacity `4` in Availability.razor** — `@slot.RemainingCapacity / 4` and the switch case `4 => "table-success"` hardcode the value that was extracted to `BookingConstants.MaxPlayersPerSlot`.

## Consider

- [ ] **`UseMigrationsEndPoint()` is misleading** — The seeder uses `EnsureCreatedAsync`, which is incompatible with EF migrations. The migrations endpoint is a no-op but implies migrations are in use.

- [ ] **`stoppingToken` not propagated in SeedWorker** — `SeedRolesAsync` and `SeedUsersAsync` don't forward the cancellation token, unlike `SeedSeasonAsync` which does.

- [ ] **`AddSeasonService` hardcodes `Guid`** — Constrains `TDbContext : IApplicationDbContext<Guid>` while `AddClubBaistServices<TKey>` is generic over `TKey`.

- [ ] **Policy names shadow role names** — Authorization policies registered with the same names as roles (`Program.cs:56-58`). Using `[Authorize(Roles = ...)]` vs `[Authorize(Policy = ...)]` gives different behavior and could confuse future developers.

- [ ] **Missing test coverage for edge cases:**
  - Duplicate player IDs in reservation creation
  - `UpdateReservationAsync` exceeding slot capacity
  - Cancelling an already-cancelled reservation

## Future Work

- [ ] **StaffConsole tee sheet: add missing columns** — The tee sheet in StaffConsole is missing four columns required by the business spec. Deferred from Area B Q15. Columns to add:
  - **Phone** — booking member's phone number (requires JOIN to `ClubBaistUser`; no schema change)
  - **Number of Carts** — carts requested per booking (requires new `NumberOfCarts` column on `TeeTimeBooking`)
  - **Employee Name** — staff member who processed the booking (requires new `EmployeeName` column on `TeeTimeBooking`)
  - **Day of Week** — derived from `TeeTimeSlotStart` (no schema change; format and display only)

- [ ] **Membership application: use sponsor email instead of GUID** — The `Apply.razor` form currently uses a GUID dropdown to select sponsors. Change this to look up members by email address instead, improving usability. Left for later.
