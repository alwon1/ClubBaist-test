# IApplicationDbContext<TKey> (Domain Data Access Contract)

## Purpose
Define a domain-facing persistence contract so application/domain services depend on an abstraction rather than a concrete EF Core `DbContext`.

## Why This Exists
- Keeps service-layer dependencies stable while allowing infrastructure implementations to vary.
- Enables easier service testing via mocks/fakes of the contract.
- Centralizes the minimum data operations required by membership application workflows.

## Current Interface Shape
- Generic contract: `IApplicationDbContext<TKey> where TKey : IEquatable<TKey>`
- Required sets:
  - `DbSet<MembershipApplication<TKey>> MembershipApplications`
  - `DbSet<MemberAccount<TKey>> MemberAccounts`
  - `DbSet<ApplicationStatusHistory<TKey>> ApplicationStatusHistories`
- Required unit-of-work method:
  - `Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)`

## Ownership and Placement
- Interface is defined in the domain project to support domain/application services.
- Concrete EF Core context implementation should live in infrastructure and implement this interface.

## Usage Guidance
- Services in this slice should request `IApplicationDbContext<TKey>` via constructor injection.
- Service logic should use the contract for query/update operations and commit using `SaveChangesAsync`.
- Identity APIs (`UserManager<IdentityUser<TKey>>`) remain separate dependencies for user lifecycle checks.

## Non-Goals (v1)
- This contract does not expose every EF Core capability.
- This contract does not model transactions explicitly beyond `SaveChangesAsync`.
- This contract does not replace Identity stores/managers.
