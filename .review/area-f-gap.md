# Area F: Cross-Cutting Concerns — Gap Analysis

## Summary

The codebase is structurally sound but carries several layers of legacy friction: a pervasive "2" suffix on project/interface/service names that serves no current purpose, an interface (`IAppDbContext2`) that is claimed to enable mock-DB testing but doesn't (tests use a real DB), missing database-level unique constraints on critical entities, fragile hard-coded IDs in seed data, and a Copper membership level that exists in the business spec but nowhere in the data model or seed. The north-star goal of "less code" is achievable here through renaming, removing the interface indirection where it doesn't add value, and simplifying the JSON value comparer boilerplate.

---

## Project Structure & Naming Issues

### "2" Suffix — Everywhere, Explains Nothing
Every project, interface, and service registration carries a "2" suffix:
- Projects: `ClubBaist.Domain2`, `ClubBaist.Services2`
- Interface: `IAppDbContext2` (in `ClubBaist.Domain2/IAppDbContext2.cs`)
- Service: `SeasonService2` (file: `ClubBaist.Services2/SeasonService2.cs`)
- Extension: `AddTeeTimeBookingServices2()`, `ServiceCollectionExtensions2.cs`
- Test project: `ClubBaist.Domain2.Tests`

There is no `Domain1` or `Services1` anywhere in the solution. The suffix is a naming remnant of a prior refactor that was never cleaned up. It adds visual noise and makes the codebase harder to introduce to new developers.

**Recommendation:** Rename all "2" suffixes away in one targeted pass. This is a pure rename with no behavior change.

### MemberShipInfo Casing
The entity is named `MemberShipInfo` (capital S mid-word). The DbSet is `MemberShips`. This is inconsistent with C# naming conventions and causes friction when reading/searching code.

**Recommendation:** Rename to `MemberInfo` (it's not "ship info", it's member information).

---

## EF Core Convention Analysis

### What Is Correctly Explicit (needs explicit config)
- Delete behaviors (`Restrict`, `SetNull`, `Cascade`) — correct, EF defaults would differ
- Join table name `BookingAdditionalParticipant` and `StandingTeeTimeAdditionalParticipant` — must be explicit
- `AutoInclude` on navigation properties — must be explicit
- JSON value conversion for `GolfRound.Scores` — must be explicit

### Missing Database Constraints
The following uniqueness rules are stated in entity comments/specs but **not configured in `OnModelCreating`**:

| Entity | Required Unique Constraint | Configured? |
|---|---|---|
| `TeeTimeBooking` | `(TeeTimeSlotStart, BookingMemberId)` | ❌ No |
| `GolfRound` | `(TeeTimeBookingId, MembershipId)` | ❌ No |
| `MemberShipInfo` | `UserId` (one-to-one unique) | ❌ No (implicit from HasOne/WithOne but not an explicit index) |

Without these, duplicate rows can be inserted if the application-layer guards fail — the DB provides no safety net. The `DuplicateBookingRule` in the booking pipeline catches this in normal flow, but concurrent requests under snapshot isolation could still race past it.

### Over-Configured
`MembershipApplication.HasOne(RequestedMembershipLevel).WithMany().OnDelete(Restrict)` — the `WithMany()` and `OnDelete(Restrict)` are explicit but would be inferred correctly by convention. Minor noise.

---

## IAppDbContext2 Analysis

**Is it used?** Yes — widely. 38 files reference it (all services, all Blazor pages that do data access, and test infrastructure).

**Does it achieve its stated purpose?** No. The XML doc comment says _"Enables testing without a real database."_ The test project (`ClubBaist.Domain2.Tests`) uses `Aspire.Hosting.Testing` with a real SQL Server container — not a mock or in-memory store. The interface provides no mock implementations.

**What it actually does:** Acts as a seam that decouples service code from the concrete `AppDbContext` type, which is reasonable for maintainability. However, because it mirrors every DbSet on AppDbContext 1:1, any change to DbContext requires updating the interface too — it's maintenance overhead with limited benefit.

**Options:**
1. Keep as-is (acceptable, low risk)
2. Remove and inject `AppDbContext` directly in services (simplest, fewer files to touch during changes)
3. Keep but remove the misleading doc comment

The interface cannot easily be removed without touching all 38 files, so option 3 (update the comment) is the lowest-cost improvement; option 2 is correct if a genuine simplification pass is done.

---

## TeeTimeEvaluation Analysis

```csharp
public record struct TeeTimeEvaluation(TeeTimeSlot Slot, int SpotsRemaining, string? RejectionReason);
```

**Is it necessary?** Yes — it is the return type of the booking rule pipeline, passed between rules as a composable LINQ projection. It bundles the slot with its computed availability and rejection reason in a single queryable value. Removing it would require returning tuples or restructuring the rule interface.

**Issues:** None significant. The record struct is appropriate and minimal.

---

## Data Model Simplification Opportunities

### MemberShipInfo vs ClubBaistUser
`ClubBaistUser` already holds personal data (FirstName, LastName, address, DOB, phone). `MemberShipInfo` holds:
- `Id` (int, 1000+ sequence) — the membership number
- `MembershipLevelId` / `MembershipLevel` FK
- `MembershipNumber` (computed: `{ShortCode}-{Id:D4}`)
- `UserId` FK (one-to-one with ClubBaistUser)

**Could they be merged?** Partially. `MembershipLevel` and `MembershipNumber` could live on `ClubBaistUser`, but the int primary key (used as `BookingMemberId`, `MembershipId` in GolfRound, `Sponsor1MemberId`, `Sponsor2MemberId`) is load-bearing across many relationships. The Guid PK of `ClubBaistUser` would need to replace it everywhere, or the int would stay as a separate field on `ClubBaistUser`.

**Cost of flattening:** High — FK changes across TeeTimeBooking, StandingTeeTime, GolfRound, MembershipApplication. Not recommended unless undertaken as a deliberate schema migration.

**Lower-cost improvement:** Keep the separation but rename `MemberShipInfo` → `Member` and fix the `DbSet` name from `MemberShips` → `Members`.

---

## Service Registration Analysis

`AddTeeTimeBookingServices2()` in `ServiceCollectionExtensions2.cs` registers:
- 5 `IBookingRule` implementations (scoped)
- `BookingService` (scoped)
- `SeasonService2` (scoped)
- `StandingTeeTimeService` (scoped)
- `ScoreService` (scoped)

`Program.cs` then adds the membership services individually:
```csharp
builder.Services.AddScoped<MembershipApplicationService>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<MembershipLevelService>();
```

**Issues:**
- Membership services are not included in the extension method — inconsistent grouping
- `IAppDbContext2` is registered in `Program.cs` as `AddScoped<IAppDbContext2>(sp => sp.GetRequiredService<AppDbContext>())` — this is fine but adds a layer

**Recommendation:** Move all domain service registrations into one `AddClubBaistServices()` extension method in the services project.

---

## Test Infrastructure Analysis

**Pattern:** A single `Domain2TestHost` (Aspire distributed test app) is created lazily and shared across all test runs via a static `SemaphoreSlim` and `Lazy<Task>`. This means all tests share one SQL Server instance and one database.

**Risks:**
- Tests that modify shared data (e.g., creating bookings) can interfere with other tests unless they use unique data
- Test ordering matters — the static host is never reset between test classes
- The shared host approach is appropriate for performance but requires tests to be self-contained (create their own data, clean up or use unique keys)

**What's tested well:** `ScoreService` (30 tests), `StandingTeeTimeService` (11 tests), some booking behavior.

**Not tested:**
- Any Blazor component / UI behavior
- `MembershipApplicationService` (no test file found for it)
- `BookingService` directly (only via `ServiceBehaviorTests`)
- Season slot generation correctness
- The advance-booking 7-day limit (rule doesn't exist)
- Any authorization/claim behavior

---

## Seed Data Analysis

**Membership levels seeded:** SH (Shareholder), SV (Silver), BR (Bronze), AS (Associate)
**Missing:** Copper/Social level — not seeded, not modeled

**Users seeded:** Admin, Committee, 3 Shareholders, 1 Silver, 1 Bronze
**Missing:**
- No Copper member (can't test Copper blocking)
- No Associate member (AS level seeded but no user)
- No Clerk/Staff user (StaffConsole and ScoreConsole are unreachable in dev without making admin)
- No Pro Shop Staff user

**Seed fragility:**
- `SeedPastBookingsAsync` uses hard-coded member IDs (`m.Id == 1`, `m.Id == 4`). If seed order changes, these silently fall through to the `LogWarning` path with no bookings created.
- Slot lookup uses exact hourly `DateTime` values (`today.AddHours(hour)`), but `SeasonService2.GenerateSlots` creates 8-minute-interval slots starting at 07:00 (07:00, 07:08, 07:16…). The hourly slot at 07:00 exists, but 08:00, 09:00, etc. may not align with any generated slot — seed bookings for `now.Hour ± N` will silently be skipped.

**Standing tee times:** None seeded — no way to exercise the standing tee time admin workflow without creating data manually.

**GolfRounds:** None seeded — MyScoreSubmissions and ScoreConsole start empty.

---

## Dead Code / Unused Elements

| Item | Location | Status |
|---|---|---|
| `AppRoles.Shareholder` role constant | `Domain2/AppRoles.cs` | Defined but never used in any `[Authorize]` and never created as an Identity role |
| Named policies (`Admin`, `MembershipCommittee`, `Member`) | `Web/Program.cs` | Registered but never referenced — all pages use `[Authorize(Roles = ...)]` directly |
| `using Microsoft.EntityFrameworkCore.Infrastructure` | `AppDbContext.cs` | Needed for `IExecutionStrategy` — not dead |
| Commented-out Azure/AppService code | `AppHost.cs` | 14 lines of commented-out Azure provisioning code should be removed |

---

## Notes

- The `GolfRound.Scores` JSON value comparer is verbose boilerplate (6 lines). EF 10 has improved primitive collection support — this may be replaceable with a simpler `[Column(TypeName = "nvarchar(max)")]` primitive collection mapping.
- `EnsureSqlServerSnapshotIsolationAsync` is a well-written utility but requires opening a separate `master` connection. This is correct and necessary.
- `OperatingHours.AllDaysDefault()` referenced in seed — this type should be reviewed to ensure defaults align with the business spec's playing hours.
