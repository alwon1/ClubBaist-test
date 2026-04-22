# Area F: Cross-Cutting Concerns — Design Analysis

## Summary

This document translates the Area F gap findings into concrete, actionable design decisions. The issues fall into three tiers of risk:

- **High impact / low risk**: "2" suffix renames, dead code removal, and seed data fixes are purely mechanical; they can be done in a single PR with no behavioral change.
- **Medium impact / moderate risk**: IAppDbContext2 simplification, service registration consolidation, and the MemberShipInfo rename each touch many files and need coordinated execution.
- **High impact / requires EF knowledge**: Missing DB constraints and GolfRound scores mapping changes must be designed carefully to avoid silent data corruption or migration failures.

All eight topics are addressed below with explicit recommendations.

---

## "2" Suffix Rename Plan

### What to rename

| Current name | New name | Type |
|---|---|---|
| `Domain2` | `Domain` | Namespace / folder |
| `Services2` | `Services` | Namespace / folder |
| `IAppDbContext2` | `IAppDbContext` | Interface |
| `SeasonService2` | `SeasonService` | Class |
| `AddTeeTimeBookingServices2` | `AddTeeTimeBookingServices` | Extension method |

### Recommended rename order

The dependency graph runs from infrastructure outward, so renames must follow that order to avoid broken intermediate states:

1. **`IAppDbContext2` → `IAppDbContext`** first, because 38 files reference this interface. Rename the interface and its single concrete implementation in the same commit. All downstream consumers are then immediately fixed by a global symbol rename.
2. **`Domain2` → `Domain`** (namespace + folder). After step 1, no file that imports `Domain2` also imports `Domain`, so the namespace collision risk is zero.
3. **`Services2` → `Services`** (namespace + folder). Same reasoning; after renaming the folder update all `using` directives.
4. **`SeasonService2` → `SeasonService`** — rename the class file and update the DI registration in `Program.cs` / extension method in the same commit.
5. **`AddTeeTimeBookingServices2` → `AddTeeTimeBookingServices`** — rename the extension method and the single call site in `Program.cs`.

### Execution notes

- Use IDE "Rename Symbol" (or `dotnet-csharp-rename`) rather than raw find-and-replace to get all reference sites automatically.
- Each of the five steps should be its own atomic commit so a broken build can be bisected.
- Run the full test suite after every step; the renames are purely cosmetic but namespace changes can silently break `typeof()` string references and XML doc `cref` attributes.

---

## IAppDbContext2 Recommendation

### Current situation

`IAppDbContext2` was introduced to allow production code to depend on an abstraction rather than the concrete `AppDbContext`, with the stated goal of enabling tests to substitute a fake. In practice, tests use the real database, so the abstraction provides no isolation benefit today.

The interface is still referenced in 38 files, meaning it is load-bearing for compilation. Removing it without a plan would be disruptive.

### Recommendation: simplify, do not remove yet

Remove the interface only when there is a concrete replacement plan for test isolation. Until then:

1. **Rename** the interface to `IAppDbContext` (covered above) so the naming debt is eliminated.
2. **Audit** whether the interface is a true subset of `DbContext` members or whether it adds application-specific query methods. If it is a pure subset, evaluate collapsing it to simply accepting `AppDbContext` directly in all 38 consumers — this is safe because `AppDbContext` is already registered in DI and every caller would receive the same concrete type.
3. **If tests are ever refactored to use EF Core's in-memory provider or SQLite**, the interface becomes genuinely useful again. In that case, keep it but restrict it to the minimal set of `DbSet<T>` properties actually needed by tests.

**Short-term**: rename only.
**Long-term**: if no in-memory/SQLite test strategy is adopted within the next sprint cycle, remove the interface entirely and inject `AppDbContext` directly. The 38-file change is mechanical and safe with IDE tooling.

---

## Missing Database Constraints

Two unique constraints are missing from the EF Core model configuration. Without them, duplicate data can be inserted that application logic cannot prevent reliably under concurrent load.

### 1. TeeTimeBooking: (TeeTimeSlotStart, BookingMemberId)

A member should not be able to hold two bookings for the same tee-time slot. The EF Fluent API configuration in `TeeTimeBookingConfiguration` (or equivalent `IEntityTypeConfiguration<TeeTimeBooking>`) should add:

```csharp
builder.HasIndex(b => new { b.TeeTimeSlotStart, b.BookingMemberId })
       .IsUnique()
       .HasDatabaseName("UQ_TeeTimeBooking_SlotStart_Member");
```

### 2. GolfRound: (TeeTimeBookingId, MembershipId)

A membership should appear at most once per booking in GolfRound. Add to `GolfRoundConfiguration`:

```csharp
builder.HasIndex(r => new { r.TeeTimeBookingId, r.MembershipId })
       .IsUnique()
       .HasDatabaseName("UQ_GolfRound_Booking_Membership");
```

### Migration notes

- Both constraints require a new `dotnet ef migrations add AddUniqueConstraints` migration.
- Before applying to any environment with existing data, run a query to detect existing violations; if any exist, a data-cleanup migration step must precede the constraint addition.
- Name the migration descriptively: `AddUniqueConstraints_TeeTimeBooking_GolfRound`.

---

## Service Registration Consolidation

### Current problem

Membership-related services are registered individually in `Program.cs` rather than inside the `AddTeeTimeBookingServices` (formerly `AddTeeTimeBookingServices2`) extension method. This splits related registrations across two locations, making it easy to miss one when adding a new service or moving to a different host project.

### Recommended `AddClubBaistServices()` shape

Create (or expand) a single top-level extension method on `IServiceCollection` that owns all application service registrations:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClubBaistServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Tee time / booking
        services.AddTeeTimeBookingServices();

        // Membership (currently scattered in Program.cs — consolidate here)
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<ISeasonService, SeasonService>();
        // ... any other membership-related registrations

        return services;
    }
}
```

`Program.cs` then becomes a single call:

```csharp
builder.Services.AddClubBaistServices(builder.Configuration);
```

### Rules going forward

- Every new `IFoo` / `FooService` pair must be registered inside the relevant domain extension method, never directly in `Program.cs`.
- The Azure host (`AppHost.cs`) and any future host projects call only `AddClubBaistServices()`, keeping host configuration minimal.

---

## Seed Data Fixes

### Problem 1: Hard-coded member IDs (`m.Id == 1`)

Hard-coding `Id == 1` assumes the seed runs against a pristine database with identity columns starting at 1, in a predictable insert order. If the seed is re-run, reordered, or run against a non-empty database, it silently targets the wrong member.

**Fix**: look up seeded members by a stable natural key (email address or membership number) rather than by generated integer ID:

```csharp
var foundingMember = context.Members
    .Single(m => m.Email == "founder@clubbaist.example");
```

Seed data should define all natural-key values as constants at the top of the seeder class so they are easy to find and update.

### Problem 2: Slot time mismatch

Seed code uses hourly increments when generating tee-time slots, but the domain models 8-minute intervals. This produces a seeded schedule that does not match production logic and will cause any test or demo that relies on seed data to fail.

**Fix**: replace the hourly loop with an 8-minute interval loop:

```csharp
var slotInterval = TimeSpan.FromMinutes(8);
var firstSlot = new TimeOnly(7, 0);
var lastSlot = new TimeOnly(19, 0);

for (var t = firstSlot; t <= lastSlot; t = t.Add(slotInterval))
{
    slots.Add(new TeeTimeSlot { Start = t });
}
```

### Problem 3: Missing Copper/Social membership level

The Copper/Social tier exists in the domain model (or is implied by gap findings) but is neither seeded nor modeled completely. Add:

- An entry in the `MembershipLevel` seed data for Copper/Social with correct pricing and entitlements.
- At least one seeded member assigned to this level, so integration tests and demos cover all tiers.

### Problem 4: No Clerk/Staff user seeded

No Clerk or Staff identity is seeded, meaning the Staff-facing workflows cannot be exercised in a development or demo environment without manual setup.

Add one seeded Clerk/Staff user with:
- A stable, well-known email address defined as a seed constant.
- The `Staff` or `Clerk` role assigned at seed time.
- A corresponding `Membership` record if the domain requires it (with a staff-level or non-billed tier).

---

## Dead Code Removal List

The following items are confirmed dead and should be deleted in a single dedicated PR to minimise noise in feature PRs:

| Item | Location | Reason dead |
|---|---|---|
| `AppRoles.Shareholder` | `AppRoles` class | No policy, no assignment, no UI check references it |
| Named authorization policy for `Shareholder` | `Program.cs` | Backs the dead `AppRoles.Shareholder`; no `[Authorize(Policy = ...)]` call site |
| Any other named policies with zero `[Authorize]` call sites | `Program.cs` | Same reason |
| Commented-out Azure resource definitions | `AppHost.cs` | Commented-out code is not a substitute for a feature flag; remove and restore via git history if needed |

**Process**: before deleting each item, do a project-wide search (including Razor views and test projects) to confirm zero live references. Document the search result in the PR description so reviewers have evidence the deletion is safe.

---

## GolfRound Scores Simplification

### Current situation

`GolfRound.Scores` stores per-hole scores as JSON via a custom `ValueConverter` (serialisation) and a verbose `ValueComparer` (change tracking). The `ValueComparer` boilerplate alone is typically 15–25 lines of lambda expressions that replicate collection equality and snapshot logic by hand.

### EF Core 8+ primitive collection mapping

EF Core 8 introduced first-class support for primitive collections (`List<int>`, `int[]`, etc.) stored as JSON columns. EF Core 10 (the current version implied by the project's target framework) further stabilises this feature.

If `Scores` is a `List<int>` (one integer score per hole), the entire `ValueConverter` + `ValueComparer` block can be replaced with a single Fluent API call:

```csharp
// In GolfRoundConfiguration
builder.Property(r => r.Scores)
       .HasColumnType("nvarchar(max)");   // EF 8+ stores List<int> as JSON automatically
```

No custom converter, no custom comparer — EF handles serialisation and change tracking internally.

**If `Scores` is a more complex type** (e.g., `List<HoleScore>` where `HoleScore` has multiple properties), use the EF Core 8 owned-entity JSON column feature instead:

```csharp
builder.OwnsMany(r => r.Scores, b =>
{
    b.ToJson();
});
```

### Migration impact

Switching from a manually serialised JSON string to EF's native JSON column should produce identical column content if the existing serialiser used the same field names. Verify by:
1. Applying the new mapping to a copy of production data.
2. Querying and round-tripping a representative set of `GolfRound` records.
3. Confirming no data loss before committing the migration.

---

## MemberShipInfo Rename Impact

### Current name

`MemberShipInfo` — incorrect casing (capital 'S' mid-word) and the suffix `Info` adds no meaning.

### Recommended new name

`Member` — this is the natural domain term. If a DTO or view model already exists named `Member` in a different namespace, use `MemberProfile` as the alternative.

### Impact assessment

The rename touches all layers of the application:

| Layer | Impact |
|---|---|
| Domain model | Rename class file; update namespace references |
| EF Core configuration | Rename `IEntityTypeConfiguration<MemberShipInfo>` to `IEntityTypeConfiguration<Member>`; update `DbSet<MemberShipInfo>` property on `AppDbContext` / `IAppDbContext` |
| Service layer | All service method signatures that accept or return `MemberShipInfo` |
| API / controller layer | All action method parameters and return types |
| Razor views / Blazor components | All `@model MemberShipInfo` declarations and `MemberShipInfo` variable names |
| Test projects | All test fixtures and assertion helpers |
| Seed data | The seeder that constructs `MemberShipInfo` instances |
| EF migration history | The rename does **not** require a new migration if only the C# class name changes and `[Table("MemberShipInfo")]` (or `.ToTable("MemberShipInfo")`) is preserved. If the table should also be renamed to `Members`, a new migration is required with a `RenameTable` call. |

### Recommended approach

1. Use IDE "Rename Symbol" to rename the class everywhere in one operation.
2. Decide separately whether to rename the database table. Renaming the table is a one-line migration but must be coordinated with any external tools or reports that query the table by name.
3. Do this rename in its own PR, after the "2" suffix renames, to keep diffs readable.

---

## Notes

- All changes described here are independent of each other except where the rename order is specified. They can be distributed across multiple PRs without blocking each other.
- The "2" suffix renames and the dead code removal are zero-risk and should be done first to reduce noise in subsequent PRs.
- The DB constraint additions carry the highest operational risk (potential data violations on existing rows) and should be preceded by a data audit query run against each environment.
- EF Core primitive collection support for `GolfRound.Scores` should be validated against a real migration round-trip before the custom ValueConverter/ValueComparer code is deleted.
- Copper/Social tier and Clerk/Staff seed additions should be treated as feature completions, not just fixes, and should be covered by integration tests that assert the seeded state.
