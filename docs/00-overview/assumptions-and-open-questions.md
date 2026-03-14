# Assumptions and Open Questions

## Current Assumptions (Membership Applications Slice)
1. We model **two core use cases** only, with alternate flows covering sub-scenarios.
2. Application outcomes are: **Accepted, Denied, OnHold, Waitlisted**.
3. Sponsor validation uses sponsor **member IDs** (no digital signatures in current scope).
4. Application status changes record `ApplicationStatusHistory` entries.
5. In the application workflow, account creation occurs when an application is accepted.
6. Legacy members may already have `MemberAccount` records without a corresponding application.

## Open Questions
1. Is applicant self-service submission required in phase 1, or is staff-assisted entry sufficient?
2. Is committee review strictly monthly in-system, or operationally monthly outside the system?
3. Should applicant notifications be in scope now, or deferred to a later slice?
