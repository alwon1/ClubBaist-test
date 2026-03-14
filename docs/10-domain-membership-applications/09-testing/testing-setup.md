# Testing Setup Guide

## Purpose
Document how service tests are wired in this repository, including the in-memory database, identity setup, and dependency injection container.

## Current Test Stack
- Test framework: MSTest
- ORM: EF Core 10
- Database provider: SQLite in-memory
- Identity: ASP.NET Core Identity with int keys

## Key Project Configuration
Test project configuration is in [ClubBaist/ClubBaist.Tests/ClubBaist.Tests.csproj](../../../ClubBaist/ClubBaist.Tests/ClubBaist.Tests.csproj).
Test project configuration is in [ClubBaist/ClubBaist.Tests/ClubBaist.Tests.csproj](../../../ClubBaist/ClubBaist.Tests/ClubBaist.Tests.csproj).

Important settings:
- Target framework: net10.0
- MSTest runner enabled
- Microsoft Testing Platform dotnet-test support enabled
- References to Domain and Services projects

Important packages:
- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- Microsoft.Extensions.DependencyInjection
- MSTest

## Test Infrastructure Files

### 1) Shared DI host and SQLite connection
File: [ClubBaist/ClubBaist.Tests/TestServiceHost.cs](../../../ClubBaist/ClubBaist.Tests/TestServiceHost.cs)

What it does:
- Runs once before all tests: initializes shared test host
- Runs once after all tests: disposes resources

### 2) Shared DI host and SQLite connection
File: [ClubBaist/ClubBaist.Tests/TestServiceHost.cs](../../ClubBaist/ClubBaist.Tests/TestServiceHost.cs)

What it does:
- Opens a single SQLite in-memory connection with Data Source=:memory:
- Registers DbContext, Identity, and services in a ServiceCollection
- Builds a ServiceProvider
- Ensures schema is created at startup

Why the connection is kept open:
- SQLite in-memory data exists only while the connection is open
- Keeping one connection open for the host lifetime preserves schema and data during test execution

### 3) Test DbContext with Identity integration
File: [ClubBaist/ClubBaist.Tests/TestApplicationDbContext.cs](../../ClubBaist/ClubBaist.Tests/TestApplicationDbContext.cs)

What it does:
- Inherits from IdentityDbContext<IdentityUser<int>, IdentityRole<int>, int>
- Implements IApplicationDbContext<int> for service compatibility
- Exposes MembershipApplications, MemberAccounts, and ApplicationStatusHistories DbSet properties
- Configures relationships and delete behavior in OnModelCreating

### 3) Setup validation test
File: [ClubBaist/ClubBaist.Tests/ServiceSetupTests.cs](../../../../ClubBaist/ClubBaist.Tests/ServiceSetupTests.cs)

What it verifies:
- DI can resolve DbContext, UserManager, and both services
- Identity user can be created
- MemberManagementService can persist a member account using the per-test isolated setup
- MemberManagementService can persist a member account using the per-test isolated setup

## Service Registration Model
The test host currently registers int-keyed services:
- IApplicationDbContext<int>
- MemberManagementService<int>
- ApplicationManagementService<int>
- UserManager<IdentityUser<int>> through IdentityCore

This mirrors production-like behavior while keeping tests fast and deterministic.

## Running Tests
From the repository root:

1. Build
   dotnet build ClubBaist/ClubBaist.slnx

2. Run tests
   dotnet test ClubBaist/ClubBaist.Tests/ClubBaist.Tests.csproj

If the local environment uses .NET 10 SDK, keep the TestingPlatformDotnetTestSupport property enabled in the test project.

## Adding New Service Tests
Recommended pattern for each test:
1. Create scope via TestServiceHost.CreateScope()
2. Resolve services from scope.ServiceProvider
3. Seed only data needed for the scenario
4. Execute service method
5. Assert both return data and persisted database state

This keeps tests focused, readable, and aligned with the existing setup.