# Tee Time Reservations – Use Case Catalog

## Scope Decision
To start backend planning with practical delivery value, Tee Time Reservations initially prioritized **2 core use cases**, while tracking additional use cases for later phases. UC-TT-04 (Standing Tee Times) has since been implemented and promoted to in-scope, bringing the total to **3 core use cases**.

## Core Use Cases (Current Focus)
1. **UC-TT-01 Create Tee Time Reservation**
2. **UC-TT-02 Review and Maintain Reservation**
3. **UC-TT-04 Manage Standing Tee Time Requests**

## Deferred / Future Use Cases (Tracked)
4. **UC-TT-03 Admin Adjust Season Window** *(deferred)*
5. **UC-TT-05 Manage Event Booking Blocks** *(deferred)*

## Coverage Mapping

| Required behavior | Covered in |
|---|---|
| Active member creates reservation | UC-TT-01 main flow |
| Season date-range validation | UC-TT-01 business rules |
| Membership-type time-of-day restrictions | UC-TT-01 validation rules |
| Shared slot capacity up to 4 total players across multiple bookings | UC-TT-01 business rules + alternates |
| View, update, cancel reservation | UC-TT-02 main + alternate flows |
| Atomic occupancy updates on edit/cancel | UC-TT-02 main flow + exceptions |
| Admin/staff reservation support | UC-TT-01 and UC-TT-02 supporting actor + alternates |
| Admin weather/operations season changes | UC-TT-03 (deferred) |
| Standing tee time lifecycle | UC-TT-04 |
| Event-based tee-sheet blocking | UC-TT-05 (deferred) |
| Audit/history emphasis | UC-TT-04 (deferred scope — ApprovedBy/ApprovedDate fields not yet stored) |
| Routine reservation audit requirements | Out of scope for UC-TT-01/UC-TT-02 in current phase |

## Use Case Status

| Use case | Status | Notes |
|---|---|---|
| UC-TT-01 Create Tee Time Reservation | In scope | Core booking flow |
| UC-TT-02 Review and Maintain Reservation | In scope | Core maintenance flow |
| UC-TT-03 Admin Adjust Season Window | Deferred | Needed for weather/operations control |
| UC-TT-04 Manage Standing Tee Time Requests | In scope | Shareholder recurring request + admin approval; allocation to tee sheet deferred |
| UC-TT-05 Manage Event Booking Blocks | Deferred | Feeds constraints into availability |

## Use Case Relationship Diagram

```mermaid
flowchart TD
  UC1[UC-TT-01 Create Tee Time Reservation] --> UC2[UC-TT-02 Review and Maintain Reservation]
  UC3[UC-TT-03 Admin Adjust Season Window - deferred] --> UC1
  UC3 --> UC2
  UC4[UC-TT-04 Manage Standing Tee Time Requests] --> AV[Availability Constraints - deferred integration]
  UC5[UC-TT-05 Manage Event Booking Blocks - deferred] --> AV
  UC1 --> CAP[Enforce slot capacity <= 4 total players]
  UC2 --> CAP