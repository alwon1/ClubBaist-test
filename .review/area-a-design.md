# Area A: Membership Applications — Design Analysis

## Summary

The membership application area is structurally thin — few files, no DTOs, services operate directly on EF entities. That is largely the right instinct, but the data model is missing most of the fields the spec requires, and two services (`MembershipLevelService`, `MembershipService`) have transaction and correctness problems that will surface when real features are added. The biggest single gap is `MembershipLevel`: with only `Name` and `ShortCode` it cannot represent fees, member type tiers, or Shareholder/Associate sub-types, which makes the entire application flow incomplete by construction.

---

## Unnecessary DTOs / Wrapper Classes

None exist — services accept and return EF entity types directly, and there are no request/response wrapper classes. This is the correct approach for this codebase and should be kept. The one area where a lightweight result type *would* be useful is `SubmitMembershipApplicationAsync`, which returns `bool` but has two semantically different failure modes (already a member vs. duplicate application). A `(bool Success, string? Reason)` tuple or small `ApplicationResult` record would allow callers to surface the right error message without re-querying. The current `bool` forces the gap-analysis bug — the caller in `Apply.razor` discards the return value partly because there is nothing actionable to do with a bare `false`.

---

## Entity Design Issues

### MembershipApplication

**Field type issues:**
- `DateOfBirth` is `DateTime` but only the date portion is meaningful. Should be `DateOnly`. Using `DateTime` risks timezone-shifted values being stored differently depending on the EF provider configuration.
- `Sponsor1MemberId` and `Sponsor2MemberId` are plain `int` with value `0` when unset (no nullable, no FK navigation, no `[ForeignKey]` annotation). EF will not enforce referential integrity and there is no way to eager-load sponsor records. These should be `int?` with proper `[ForeignKey]` attributes pointing to `MemberShipInfo` (or `ClubBaistUser`), with nullable navigation properties.

**Missing fields (spec requirements):**
- `ApplicantSignature` (string) — the physical form has a signature line; the digital equivalent is a checkbox or typed name that must be persisted.
- `ApplicationDate` / `SignedDate` (`DateOnly` or `DateTime`) — required for the record of when the applicant attested.
- `SubmittedAt` (`DateTimeOffset`) — auditing when the record entered the system.
- No `City` or `Province` fields; `Address` is a single unstructured string. The approval method in `MembershipApplicationService` copies `Address` to `ClubBaistUser.AddressLine1` and hard-codes `City = "Unknown"` and `Province = "Unknown"`, which is a clear sign the application entity is under-specified.

**Validation gaps:**
- `DateOfBirth` has no minimum age validator. The attribute is present but there is no `[Range]` or custom `[MinimumAge]` attribute. Age eligibility is entirely absent.
- `Sponsor1MemberId` / `Sponsor2MemberId` are not validated as pointing to actual active members anywhere in the entity or service.

**Namespace inconsistency:** `MembershipApplication` lives in `ClubBaist.Domain2.Entities.Membership` but `MemberShipInfo` and `MembershipLevel` live in `ClubBaist.Domain2` (root namespace). This is inconsistent and makes `using` statements awkward.

### MemberShipInfo

**Naming:** The class is spelled `MemberShipInfo` (capital S mid-word). All other types use `Membership` as a single word. This is a typo that propagates into every `using`, DbSet name (`db.MemberShips`), and log message throughout the codebase.

**Necessity of a separate entity vs. flattening onto `ClubBaistUser`:** Keeping `MemberShipInfo` as a separate entity is correct — it models a 1-to-1 optional relationship (not every `ClubBaistUser` is a member), and it holds the FK to `MembershipLevel`. Flattening membership fields onto `ClubBaistUser` would pollute the identity record and make non-member users carry null columns. The entity should stay, but should be renamed `MembershipInfo` (or simply `Membership`) and moved to the `ClubBaist.Domain2.Entities.Membership` namespace.

**`Id` range constraint:** `[Range(1000, int.MaxValue)]` on the primary key is unusual. The range constraint is a data annotation validator, not a database constraint; EF will still insert IDs starting at 1 unless a `DBCC CHECKIDENT` or sequence is configured separately. The intent (to produce 4-digit member numbers via `MembershipNumber`) is undermined because the computed property `$"{MembershipLevel.ShortCode}-{Id:D4}"` will format IDs below 1000 as 4 digits anyway (e.g. `SH-0001`), and IDs of 10000+ will exceed 4 digits silently. The constraint should be removed or the member-number format adjusted.

**`MembershipNumber` not stored:** `MembershipNumber` is `[NotMapped]` and computed from `ShortCode` and `Id`. If `ShortCode` is changed after a member has been issued a membership number, their number silently changes. Consider storing `MembershipNumber` as a persisted generated column or computing it once at creation and storing it.

### MembershipLevel

This is the most under-specified entity in the codebase. It currently has `Id`, `ShortCode`, `Name`, and a tee-time availability collection. Everything fee- and tier-related is absent.

**Fields required to implement the spec:**
- `AnnualFee` (`decimal`) — the core pricing field, completely missing.
- `InitiationFee` (`decimal`) — typically separate from the annual fee.
- `MemberType` (enum: `Shareholder`, `Associate`, or similar) — the spec distinguishes these sub-types but the model has no discriminator.
- `IsActive` (`bool`) — levels should be deactivatable without deletion.
- `MaxCapacity` (`int?`) — Shareholder levels often have a fixed cap; needed for waitlist logic.
- `MinimumAge` / `MaximumAge` (`int?`) — if eligibility rules are per-level (e.g. junior memberships).

The simplest model that covers the spec adds these five or six scalar columns to `MembershipLevel` rather than creating a new inheritance hierarchy.

---

## Service Design Issues

### MembershipApplicationService

**`SubmitMembershipApplicationAsync` — no transaction:** Adding the application record and checking for duplicates are two separate operations. A concurrent submission between the `AnyAsync` check and the `Add`/`SaveChangesAsync` can insert two applications for the same email. The duplicate check and the insert should be inside a serializable or snapshot transaction with a unique index on `(Email, Status != Denied)` — or, more practically, a unique filtered index on `Email` where `Status != Denied` enforced at the database level.

**`ApproveMembershipApplicationAsync` — hard-coded password and city/province:**
- Line 86: `"ChangeMe123!"` is hard-coded as the initial password. This is a security issue and a maintenance problem. It should come from configuration (e.g. `IOptions<MembershipSettings>`).
- Lines 81–82: `City = "Unknown"` and `Province = "Unknown"` are placeholders that will be persisted to the database because the application entity lacks these fields. Once `MembershipApplication` gains structured address fields this code must be updated.

**`ApproveMembershipApplicationAsync` — role existence check inside transaction:** The `roleManager.RoleExistsAsync` / `roleManager.CreateAsync` block (lines 98–111) runs inside the snapshot transaction but operates on Identity tables that may be managed by a different transaction context. If role creation fails the transaction rollback may not cover the Identity side-effects, depending on the EF provider. Role seeding should happen at startup (via a seeding service), not lazily inside a business transaction.

**`ApproveMembershipApplicationAsync` — `membershipLevelId` parameter is redundant:** The application already carries `RequestedMembershipLevelId`. The separate `membershipLevelId` parameter implies an admin can approve at a different level than requested, which may be intentional but is not documented. If it is intentional, the difference should be recorded on the application entity (e.g. `ApprovedMembershipLevelId`).

**`DenyApplicationAsync` vs `SetApplicationStatusAsync` duplication:** `DenyApplicationAsync` is a thin wrapper around status mutation. `SetApplicationStatusAsync` intentionally refuses to set terminal statuses but then `DenyApplicationAsync` sets `Denied` outside of it. This creates two parallel paths for status mutation with slightly different guard logic. Consolidate into one internal `SetStatusAsync` helper that all public methods call.

**Sponsor validation is absent:** `SubmitMembershipApplicationAsync` does not verify that `Sponsor1MemberId` and `Sponsor2MemberId` refer to real, active members. This is the core business rule for the application process and is completely unenforced.

### MembershipService

**`SetMembershipLevelForUserAsync` — ignores `SaveChangesAsync` result:** Line 32 `await db.SaveChangesAsync()` does not check whether any rows were affected before returning `true`. If EF's change-tracker somehow had no changes, `true` is returned falsely. Should be `return await db.SaveChangesAsync() > 0`.

**`SetMembershipLevelForUserAsync` — no transaction:** Reading `MemberShipInfo` and writing `MembershipLevelId` on it are two operations. Wrap in a transaction or use `ExecuteUpdateAsync` directly for a single round-trip.

### MembershipLevelService

**`CreateMembershipLevelAsync` — transaction is unnecessary:** A single-entity insert with `SaveChangesAsync` is already atomic. Wrapping it in an explicit snapshot transaction and execution strategy adds overhead and complexity for no benefit. The transaction is only warranted when multiple writes must be coordinated.

**`UpdateMembershipLevelAsync` overloads — the entity overload is a leaky convenience:** `UpdateMembershipLevelAsync(MembershipLevel)` extracts `Id`, `Name`, and `ShortCode` and delegates to the `(int, string, string)` overload. This means the caller can pass a tracked entity but the method will detach and re-fetch it from the database, wasting a round-trip and risking stale-data overwrites. The entity overload should instead mutate the passed entity directly if it is tracked, or be removed in favour of callers using the `(int, string, string)` form explicitly.

**No `GetAllMembershipLevelsAsync`:** `MembershipLevelService` has no read methods. Callers must query `db.MembershipLevels` directly, bypassing the service layer. A simple `GetAllAsync()` returning `IReadOnlyList<MembershipLevel>` belongs here.

---

## Convention Opportunities

1. **Primary constructor pattern** — all three services already use primary constructors (`MembershipApplicationService(IAppDbContext2 db, ...)`) consistently. Continue this pattern; do not introduce field-based injection.

2. **`ExecutionStrategy` helper** — `CreateExecutionStrategy` + `BeginTransactionAsync` appears in both `MembershipApplicationService.ApproveMembershipApplicationAsync` and both methods of `MembershipLevelService`. Extract a protected/internal `ExecuteInTransactionAsync<T>(Func<Task<T>> work, IsolationLevel level)` extension method on `IAppDbContext2` to eliminate the boilerplate.

3. **`DateOnly` for date-only fields** — `DateOfBirth` on both `MembershipApplication` and `ClubBaistUser` should be `DateOnly`. EF Core 6+ supports `DateOnly` natively with SQL Server's `date` column type, avoiding timezone issues.

4. **Structured address type** — `Address` (single string) appears in `MembershipApplication` and multiple fields appear in `ClubBaistUser` (`AddressLine1`, `City`, `Province`, `PostalCode`). A shared `Address` value object (owned entity) on both would remove duplication and make the approval-copy code in `ApproveMembershipApplicationAsync` trivial.

5. **`ApplicationStatus` terminal-state guard** — the guard `if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.Denied)` is copy-pasted in both `ApproveMembershipApplicationAsync` and `DenyApplicationAsync`. Add an `IsTerminal` extension method or property to `ApplicationStatus` or the entity itself.

6. **`[Authorize]` on `Apply.razor`** — the gap analysis already flagged this. As a convention, all Razor pages/components under the membership application flow should declare authorization requirements at the component level, and the `Program.cs` fallback policy should be `RequireAuthenticatedUser` so missing attributes fail closed rather than open.

---

## Recommended Data Model Changes

### Add to `MembershipLevel`
| Field | Type | Notes |
|---|---|---|
| `MemberType` | `enum MemberType { Shareholder, Associate }` | Core tier discriminator |
| `AnnualFee` | `decimal` | Required for fee model |
| `InitiationFee` | `decimal` | May be 0 for some levels |
| `MaxCapacity` | `int?` | Null = unlimited (Associate) |
| `IsActive` | `bool` | Soft-delete / retire old levels |

### Add to `MembershipApplication`
| Field | Type | Notes |
|---|---|---|
| `DateOfBirth` | `DateOnly` | Change from `DateTime` |
| `City` | `string` | Structured address |
| `Province` | `string` | Structured address |
| `Sponsor1MemberId` | `int?` | Make nullable, add FK |
| `Sponsor2MemberId` | `int?` | Make nullable, add FK |
| `ApplicantSignature` | `string` | Typed-name attestation |
| `SignedDate` | `DateOnly` | Date of signature |
| `SubmittedAt` | `DateTimeOffset` | Audit timestamp, set by service |
| `ApprovedMembershipLevelId` | `int?` | If admin can override requested level |

### Rename
- `MemberShipInfo` → `MembershipInfo` (fix capitalisation, move to `Entities.Membership` namespace)
- DbSet `db.MemberShips` → `db.Memberships`

### Remove / Reconsider
- `[Range(1000, int.MaxValue)]` on `MembershipInfo.Id` — replace with a proper database sequence if 4-digit IDs are required, or store `MembershipNumber` as a persisted string set at creation time.
- Hard-coded `"ChangeMe123!"` in `ApproveMembershipApplicationAsync` — move to `IOptions<MembershipApprovalOptions>`.

---

## Notes

- The `MembershipLevelTeeTimeAvailability` entity in `MembershipLevel.cs` is well-structured and unrelated to the application-area gaps. No changes recommended there.
- `MembershipService.GetMembershipLevelForUserAsync` has two overloads (by entity and by `Guid`) which is a clean pattern; keep it.
- There are no unit tests visible for any of these services. The transaction-heavy `ApproveMembershipApplicationAsync` method especially warrants integration tests with an in-memory or Sqlite provider before further changes are made to it.
- The `ApplicationStatus.Waitlisted` value exists in the enum but no code path currently sets it. If the Shareholder tier has a capacity cap, waitlist logic will need a `WaitlistMembershipApplicationAsync` method analogous to `DenyApplicationAsync`.
