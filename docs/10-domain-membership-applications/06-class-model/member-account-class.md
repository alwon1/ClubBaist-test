# MemberAccount (Domain Class)

## Purpose
Represents the persisted member profile/account record used for member-specific business data.

## Responsibilities
- Store member-specific profile and account data.
- Link member data to ASP.NET Identity user (`ApplicationUser`).
- Provide a canonical data record for downstream membership/finance features.

## Core Properties
- `MemberAccountId` (Guid/int)
- `ApplicationUserId` (generic key type `TKey`)
- `ApplicationUser` (`ApplicationUser<TKey>` navigation)
- `MemberNumber` (club member identifier)
- `FirstName`
- `LastName`
- `DateOfBirth`
- `Email`
- `Phone`
- `AlternatePhone` (nullable)
- `Address`
- `PostalCode`
- `MembershipCategory` (`Shareholder | Associate | Other future categories`)
- `IsActive` (bool)
- `CreatedAt` (`DateTime`)
- `UpdatedAt` (`DateTime`)

## Optional (if needed now)
- None currently.

## Invariants (rules that must always be true)
- `ApplicationUserId` is required.
- `MemberNumber` is required and unique.
- `FirstName`, `LastName`, and `Email` are required.
- `CreatedAt` is required.

## Deferred / Future (currently unplanned)
- Financial ledger/billing fields and payment schedules.
- Additional membership metadata for expanded membership tiers.

## Explicit Non-Rule (current design)
- A `MemberAccount` does not require a corresponding `MembershipApplication` record (legacy members may predate the system).

## Allowed State Changes (v1)
- `IsActive: true -> false` (deactivation)
- `IsActive: false -> true` (reactivation)
- Profile/contact fields may be updated over time.

## Key Methods (conceptual)
- Data-only model; no domain behavior methods required in current scope.
