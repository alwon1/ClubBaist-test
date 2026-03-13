# Membership Applications – Initial Component View

## Purpose
Provide a planning-level component diagram that remains consistent with the two core use cases.

```mermaid
flowchart LR
  UI[Blazor Server UI]
  APP[Membership Application Service]
  RULES[Sponsor & Validation Rules]
  REVIEW[Committee Review/Decision Service]
  AUDIT[Audit & Status History Store]
  ACCOUNT[Member Account Service]
  DATA[(Application Data Store)]

  UI --> APP
  UI --> REVIEW
  APP --> RULES
  APP --> DATA
  REVIEW --> DATA
  REVIEW --> AUDIT
  REVIEW --> ACCOUNT
```

## Mapping to Use Cases
- **UC-MA-01 Submit Membership Application**: UI, Membership Application Service, Sponsor & Validation Rules, Application Data Store.
- **UC-MA-02 Review and Decide Membership Application**: UI, Committee Review/Decision Service, Audit & Status History Store, Member Account Service (accepted outcomes).
