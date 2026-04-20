# Proposed UI Design Diagrams (Low-Fidelity)

These diagrams are intentionally low-fidelity and are meant to support planning conversations before implementation.

## 1) Site Map and Role-Based Navigation

```mermaid
flowchart TD
    A[Home / Dashboard] --> B[Membership Applications]
    A --> C[Tee Time Reservations]
    A --> D[Player Scores]

    B --> B1[New Membership Application]
    B --> B2[Application Submission Confirmation]
    B --> B3[My Membership Application Status]
    B --> B4[Membership Application Inbox]
    B4 --> B5[Membership Application Review Workspace]
    B5 --> B6[Membership Decision Audit Trail]

    C --> C1[Tee Time Availability Search]
    C1 --> C2[Create Reservation]
    C2 --> C3[Reservation Confirmation]
    C --> C4[My Reservations]
    C4 --> C5[Reservation Detail / Maintenance]
    C --> C6[Staff Reservation Console]

    D --> D1[My Score Submissions\nEligible Bookings List]
    D1 --> D2[Score Entry Form\n18-Hole Scorecard]
    D2 --> D3[Submission Confirmation]
    D --> D4[Score Entry Schedule Console\nToday's Completed Tee Times]
    D --> D5[Member Lookup\nSearch by Name / ID]
    D4 -->|Click player| D2
    D5 -->|Member found| D2

    classDef member fill:#E8F3FF,stroke:#3B82F6,color:#0F172A;
    classDef admin fill:#FEF3C7,stroke:#D97706,color:#0F172A;
    classDef shared fill:#ECFDF5,stroke:#059669,color:#0F172A;

    class A shared;
    class B1,B2,B3,C1,C2,C3,C4,C5,D1,D2,D3 member;
    class B4,B5,B6,C6,D4,D5 admin;
```

## 2) Membership Application Flow (Applicant + Committee)

```mermaid
flowchart LR
    AP[Applicant] --> F1[New Membership Application\nForm + Validation]
    F1 --> F2[Submission Confirmation]
    F2 --> F3[My Application Status]

    CL[Clerk/Committee] --> W1[Application Inbox\nTable + Filters]
    W1 --> W2[Review Workspace\nDetail + Decision Form]
    W2 --> D{Decision}
    D -->|Accept| S1[Status: Accepted]
    D -->|Deny| S2[Status: Denied]
    D -->|On Hold| S3[Status: OnHold]
    D -->|Waitlist| S4[Status: Waitlisted]

    S1 --> A1[Audit Trail Entry]
    S2 --> A1
    S3 --> A1
    S4 --> A1

    A1 --> F3
```

## 3) Tee-Time Reservation Flow (Member + Staff)

```mermaid
flowchart LR
    M[Member] --> T1[Availability Search\nDate + Slots Table]
    T1 --> T2[Create Reservation\nGuided Form]
    T2 --> T3[Reservation Confirmation]
    T3 --> T4[My Reservations\nList/Table]
    T4 --> T5[Reservation Detail / Maintenance]

    S[Admin/Clerk] --> T6[Staff Reservation Console\nMember Search + Master Detail]
    T6 --> T2
    T6 --> T5
```

## 4) Wireframe – Membership Application Inbox

```text
+--------------------------------------------------------------------------------+
| Membership Application Inbox                                                   |
+--------------------------------------------------------------------------------+
| Filters: [Status v] [Review Cycle v] [Date Range] [Search Applicant......]    |
| Actions: [Export] [Assign Reviewer]                                            |
+--------------------------------------------------------------------------------+
| #   Applicant Name      Category   Submitted On   Status      Reviewer         |
| 42  Alex Rivers         Family     2026-03-02     Submitted   Unassigned       |
| 41  Priya Das           Individual 2026-03-01     OnHold      J. Lee           |
| 40  Morgan Smith        Family     2026-02-28     Waitlisted  K. Patel         |
+--------------------------------------------------------------------------------+
| [Open Selected] [Refresh]                                                      |
+--------------------------------------------------------------------------------+
```

## 5) Wireframe – Membership Review Workspace

```text
+--------------------------------------------------------------------------------+
| Membership Application Review Workspace                                         |
+-------------------------------------+------------------------------------------+
| Application Detail (read-only)      | Decision Panel                           |
| - Applicant profile                 | Decision: ( ) Accept ( ) Deny            |
| - Sponsor details                   |           ( ) OnHold ( ) Waitlist        |
| - Declarations / consents           | Rationale: [...........................]  |
| - Validation flags                  | Internal note: [.......................] |
|                                     | [Save Note] [Submit Decision]            |
+-------------------------------------+------------------------------------------+
| Status History / Audit Timeline                                                |
| - Submitted by applicant (date/time)                                            |
| - Assigned to reviewer (date/time)                                              |
| - Previous decision notes...                                                    |
+--------------------------------------------------------------------------------+
```

## 6) Wireframe – Tee-Time Availability + Booking

```text
+--------------------------------------------------------------------------------+
| Tee Time Availability Search                                                    |
+--------------------------------------------------------------------------------+
| Date: [2026-05-18]  Players: [2]  Time Window: [Morning v]  [Search Slots]     |
+--------------------------------------------------------------------------------+
| Tee Time   Capacity   Booked   Open Spots   Restrictions                        |
| 07:30      4          2        2           Member only                          |
| 07:40      4          4        0           Full                                 |
| 07:50      4          1        3           -                                    |
+--------------------------------------------------------------------------------+
| [Select Slot] -> opens Create Reservation form                                  |
+--------------------------------------------------------------------------------+

+--------------------------------------------------------------------------------+
| Create Reservation (selected slot: 07:50)                                       |
+--------------------------------------------------------------------------------+
| Booking Member: [Current User v]                                                |
| Additional Players: [Name] [Name] [Add Player]                                  |
| Policy Checks: ✔ within season  ✔ within booking window                         |
| [Confirm Reservation] [Cancel]                                                  |
+--------------------------------------------------------------------------------+
```
