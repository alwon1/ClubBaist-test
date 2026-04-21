# Area F: Cross-Cutting Concerns — Prioritized Task List

## Critical (data integrity / correctness)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| C1 | Add unique DB index `(TeeTimeSlotStart, BookingMemberId)` on `TeeTimeBooking` | H | S | Without this, concurrent requests can race past `DuplicateBookingRule` and insert duplicate rows. Run a violation-check query per environment before applying migration. |
| C2 | Add unique DB index `(TeeTimeBookingId, MembershipId)` on `GolfRound` | H | S | Same concurrent-race risk. Bundle both constraints into a single descriptive migration: `AddUniqueConstraints_TeeTimeBooking_GolfRound`. |
| C3 | Fix seed slot-time mismatch: replace hourly loop with 8-minute-interval loop | H | S | Current hourly increments produce slots that don't match the 8-min domain logic; seeded bookings silently fall through. Fix by looping `TimeSpan.FromMinutes(8)` from 07:00. |
| C4 | Fix seed hard-coded member IDs (`m.Id == 1`, `m.Id == 4`) | H | S | Look up seeded members by stable natural key (email constant) instead of generated int ID. Silent failures when seed order changes. |

---

## High Priority (naming / dead code / conventions)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| H1 | Rename `IAppDbContext2` → `IAppDbContext` (step 1 of "2" suffix pass) | M | S | 38-file blast radius; use IDE "Rename Symbol". Must be first in rename order — all downstream steps depend on it. Own atomic commit; run tests after. |
| H2 | Rename `Domain2` namespace/folder → `Domain` (step 2) | M | S | After H1, no namespace collision risk. Update all `using` directives. Own atomic commit. |
| H3 | Rename `Services2` namespace/folder → `Services` (step 3) | M | S | After H2. Same approach. Own atomic commit. |
| H4 | Rename `SeasonService2` → `SeasonService` and update DI registration (step 4) | M | XS | Single class file + one DI call site. Bundle with H3 commit or own commit. |
| H5 | Rename `AddTeeTimeBookingServices2()` → `AddTeeTimeBookingServices()` and update `Program.cs` call site (step 5) | M | XS | One method + one call site. Final step of "2" pass. |
| H6 | Remove dead `AppRoles.Shareholder` constant and its named authorization policy in `Program.cs` | M | XS | Zero live references confirmed by project-wide search. Document search evidence in PR. |
| H7 | Remove all other named authorization policies in `Program.cs` with zero `[Authorize(Policy=...)]` call sites | M | XS | Verify via project-wide search including Razor/Blazor files before deleting. Bundle with H6 in a single "dead code" PR. |
| H8 | Remove 14 lines of commented-out Azure resource definitions in `AppHost.cs` | L | XS | Not a feature flag substitute; recoverable via git history. Bundle with H6/H7. |
| H9 | Rename `MemberShipInfo` → `Member`; rename `DbSet<MemberShipInfo> MemberShips` → `DbSet<Member> Members` | M | M | Touches domain, EF config, services, Blazor components, tests, seed. Use IDE rename. Decide separately whether to issue a `RenameTable` migration (one-line but coordinate with any external reporting queries). Do this PR after the "2" suffix renames. |
| H10 | Fix NavMenu: hide "My Standing Requests" link from non-Shareholder members (guard on `standing-tee-time.book` claim, not just `AppRoles.Member`) | H | XS | Members without the claim see a link they cannot use. Simple `AuthorizeView` claim check. |
| H11 | Fix NavMenu: hide "Standing Tee Time" home dashboard card from non-Shareholder members | H | XS | Same problem on the home page. Matches H10 fix pattern. |

---

## Medium Priority (design improvements)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| M1 | Consolidate all service registrations into a single `AddClubBaistServices()` extension method; remove individual membership service registrations from `Program.cs` | M | S | Membership services currently split across `Program.cs` and the extension method. All future `IFoo`/`FooService` pairs go into the extension method only. |
| M2 | Add Copper/Social membership level to seed data with correct pricing and entitlements | M | S | Implied by domain spec; currently missing from both model and seed. Add at least one seeded Copper member so all tier-based rules are exercisable in dev/test. Treat as a feature completion, cover with integration tests. |
| M3 | Add seeded Clerk/Staff user (stable email constant, `Staff`/`Clerk` role assigned at seed time) | M | S | Staff Console and Score Console are unreachable in dev without logging in as Admin. Add a corresponding membership record if the domain requires it. |
| M4 | Add seeded Associate member (AS level already seeded but no user exists) | L | XS | Cannot test Associate-tier rules without a seeded user. Bundle with M2/M3 in a "seed completeness" PR. |
| M5 | Remove misleading XML doc comment from `IAppDbContext` (formerly `IAppDbContext2`) claiming it enables testing without a real DB | M | XS | The test project uses Aspire with a real SQL Server container — the comment is factually wrong and misleading. Do in same commit as H1. |
| M6 | Simplify `GolfRound.Scores` mapping: replace custom `ValueConverter` + `ValueComparer` boilerplate with EF Core 8+ native primitive collection mapping (`HasColumnType("nvarchar(max)")`) | M | M | Verify migration round-trip against a copy of real data before removing the old converter. If `Scores` is `List<HoleScore>` (complex type), use `OwnsMany(...).ToJson()` instead. |
| M7 | Remove `WithMany()` and `OnDelete(Restrict)` from `MembershipApplication.HasOne(RequestedMembershipLevel)` — both are correctly inferred by EF convention | L | XS | Minor noise reduction in `OnModelCreating`. Verify convention behaviour before removing. |
| M8 | Add explicit unique index for `MemberShipInfo.UserId` (the one-to-one FK) to `OnModelCreating` | M | S | Currently implicit from `HasOne/WithOne`; an explicit index makes the constraint visible and documents the invariant. Pair with C1/C2 migration or own follow-up migration. |
| M9 | Fix NavMenu username display: show `FirstName LastName` instead of email address (`@context.User.Identity?.Name`) | M | S | Requires resolving display name from user claims or a service call at layout render time. |
| M10 | Add global `ErrorBoundary` per component in addition to the `/Error` fallback page | M | M | Unhandled exceptions in a Blazor component currently crash the whole circuit. Wrap pages with component-level `<ErrorBoundary>`. |
| M11 | Add a "Record Score" direct nav link for members | L | XS | Currently only reachable via My Scores or home dashboard. One `NavLink` addition. |

---

## Low Priority (future / Fluent UI shell)

| # | Task | Impact | Effort | Note |
|---|---|---|---|---|
| L1 | Replace `MainLayout.razor` + `NavMenu.razor` with Fluent UI shell (`FluentLayout`, `FluentHeader`, `FluentNavMenu`, `FluentNavGroup`) | H | L | First change in any Fluent UI migration; unblocks all subsequent page rewrites. Requires `Microsoft.FluentUI.AspNetCore.Components` NuGet, `AddFluentUIComponents()`, and CSS/JS imports. |
| L2 | Add `<FluentToastProvider>` to `Routes.razor`/`App.razor` for global success/error notifications | H | S | Depends on L1 (Fluent package installed). Replaces per-page text feedback. |
| L3 | Add `<FluentDialogProvider>` to `Routes.razor`/`App.razor` and replace inline `@if` confirmation patterns | M | S | Depends on L1. |
| L4 | Replace static Bootstrap card home page with role-conditional `<FluentGrid>` of `<FluentCard>` dashboard tiles | H | L | Member sees next tee time, record score, standing request (Shareholder only); Staff sees today's tee sheet; Committee sees pending applications; Admin sees all. Depends on L1. |
| L5 | Replace Bootstrap `QuickGrid` data tables with `<FluentDataGrid>` (sorting, pagination, virtualization) | M | XL | Wide surface area — every grid-bearing page. Do page by page after L1. |
| L6 | Add `<FluentProgressRing>` or `<FluentSkeleton>` loading states to async operations (booking, score submit) | M | M | Depends on L1. Currently no loading feedback. |
| L7 | Introduce role-based nav grouping structure (Golf / Account / Staff / Admin groups) with correct claim guards per the role-nav table in the UI report | H | M | Depends on L1. Addresses flat-list nav and multiple role-visibility bugs simultaneously. |
| L8 | Replace `bi bi-*` Bootstrap icons with `<FluentIcon>` throughout | L | M | Depends on L1. Cosmetic consistency; do last. |
| L9 | Evaluate removing `IAppDbContext` entirely and injecting `AppDbContext` directly into all 38 consumers | M | L | Only warranted if no EF in-memory/SQLite test strategy is adopted. Mechanical 38-file change; safe with IDE tooling. Defer until after test strategy is decided. |
| L10 | Evaluate merging `MemberShipInfo`/`Member` fields into `ClubBaistUser` (flattening) | L | XL | High cost — FK changes across TeeTimeBooking, StandingTeeTime, GolfRound, MembershipApplication. Not recommended unless undertaken as a deliberate schema migration sprint. |
| L11 | Expose Register link in NavMenu with explanation (or document why it is intentionally hidden) | L | XS | Currently commented out with no explanation. If self-registration is disabled by design, add a code comment; if it should be visible to guests, uncomment and test. |

---

## Grouped Tasks (must go together)

**Group G1 — "2" Suffix Rename Pass** (H1 → H2 → H3 → H4 → H5, in order)
Each step in the rename order depends on the previous. Break into five atomic commits so a broken build can be bisected. Also include M5 (fix misleading doc comment) in the H1 commit.

**Group G2 — DB Constraint Migration** (C1 + C2 + M8)
All three unique constraints belong in a single migration (`AddUniqueConstraints_TeeTimeBooking_GolfRound_Member`). Run violation-detection queries per environment before applying. Optionally include C3/C4 seed fixes if the seed environment needs cleaning first.

**Group G3 — Dead Code PR** (H6 + H7 + H8)
All confirmed-zero-reference deletions. Bundle into one PR. Document project-wide search evidence for each deletion in the PR description.

**Group G4 — Seed Completeness PR** (C3 + C4 + M2 + M3 + M4)
All seed fixes and additions are interdependent in the seeder file. Fix timing/ID fragility first, then add missing tiers and users. Cover with integration tests asserting seeded state.

**Group G5 — Fluent UI Shell + Providers** (L1 + L2 + L3)
`FluentToastProvider` and `FluentDialogProvider` must be registered in the same PR that installs the Fluent package and replaces the layout. Without L1, L2 and L3 have no package to depend on.

**Group G6 — NavMenu Role Visibility Fixes** (H10 + H11 + M9 + M11)
All NavMenu/home-page display fixes touch the same files (`NavMenu.razor`, `Home.razor`). Batch them to avoid repeated small PRs on the same components.

---

## Independent Tasks (can be parallelized)

The following tasks have no dependency on each other or on any group above and can be assigned to different developers simultaneously:

| Task | Depends on | Parallelizable with |
|---|---|---|
| G3 (dead code removal) | Nothing | G2, G4, G6 |
| G4 (seed completeness) | Nothing (but seed should be applied to clean DB) | G2, G3, G6 |
| G6 (NavMenu role fixes) | Nothing | G2, G3, G4 |
| M6 (GolfRound Scores EF simplification) | Nothing (verify round-trip first) | All groups |
| M7 (remove over-configured EF noise) | Nothing | All groups |
| G1 (rename pass) | Nothing except internal ordering | G2, G3, G4, G6 — can run in parallel but H9 (MemberShipInfo rename) should follow G1 to keep diffs readable |
| H9 (MemberShipInfo → Member rename) | G1 complete (for readable diffs) | G2, G3, G4, G6 |
| M10 (ErrorBoundary per component) | Nothing | All groups |
| G5 + L4-L8 (Fluent UI work) | L1 must be done first within the group | All non-UI groups |
| L9 (evaluate removing IAppDbContext) | Test strategy decision | Everything else |
| L10 (flatten MemberShipInfo into ClubBaistUser) | Deliberate schema sprint decision | Everything else |
