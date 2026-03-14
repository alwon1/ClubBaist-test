# Membership Applications – Initial Component View

## Purpose
Provide a planning-level component diagram aligned with the current service model.

```mermaid
flowchart LR
  UI[Blazor Server UI]
  AMS[ApplicationManagementService]
  MMS[MemberManagementService]
  RULES[Sponsor & Validation Rules]
  AUDIT[ApplicationStatusHistory Store]
  DATA[(Application Data Store)]
  MEMBER[(MemberAccount Store)]

  UI --> AMS
  AMS --> RULES
  AMS --> DATA
  AMS --> AUDIT
  AMS --> MMS
  MMS --> MEMBER
```

## Mapping to Use Cases
- **UC-MA-01 Submit Membership Application**: UI, `ApplicationManagementService`, Sponsor & Validation Rules, Application Data Store.
- **UC-MA-02 Review and Decide Membership Application**: UI, `ApplicationManagementService`, ApplicationStatusHistory Store, and `MemberManagementService` for accepted outcomes.
