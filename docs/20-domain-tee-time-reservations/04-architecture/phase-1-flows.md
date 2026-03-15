# Phase 1 Service Flows (High-Level)

This document defines three sequence-style, implementation-agnostic flows for tee-time reservations. The steps are explicit enough to derive service interfaces while remaining neutral on protocols, storage, and deployment details.

## Terminology and service-name mapping
To keep flow language implementation-agnostic while still aligning with canonical service names in `service-architecture-and-delivery-order.md`, use the following mappings:

- **Reservation Policy Service** -> **BookingPolicyService**
- **Season/Calendar Service** -> **SeasonService**
- **Slot Inventory Service** -> **AvailabilityService** internal slot-inventory component (or a separate **SlotInventoryService** if extracted later)

In the sequences below, canonical names are used directly to reduce ambiguity.


## 1) Availability query flow

### Purpose
Return reservable tee-time slots for a caller-provided search window, party requirements, and policy context.

### Sequence
1. **Caller -> Availability API**
   - **Inputs:** course/facility identifier, requested date or date range, preferred start/end time window, requested party size, optional player segment (member/guest), optional locale/time zone, correlation/request ID.
   - **Outputs:** normalized query object and request acknowledgment metadata.
2. **Availability API -> SeasonService**
   - **Inputs:** facility identifier, requested date range.
   - **Outputs:** season window(s), open/closed flags, blackout periods, holiday/event constraints.
3. **Decision: SeasonService validation fails**
   - If requested date range is outside valid season/calendar constraints, return **no availability + season-invalid reason** to caller.
4. **Availability API -> AvailabilityService slot-inventory component**
   - **Inputs:** facility identifier, filtered date/time range, party size constraints.
   - **Outputs:** candidate slots with current capacity/remaining inventory and slot metadata.
5. **Availability API -> BookingPolicyService**
   - **Inputs:** caller segment, party size, timing context, candidate slot set.
   - **Outputs:** per-slot policy eligibility (allowed/disallowed) and policy reason codes.
6. **Availability API -> Caller**
   - **Outputs:** available slot list (only slots passing SeasonService + BookingPolicyService checks), optional excluded-slot reasons, response timestamp/version token for client-side staleness handling.

## 2) Reservation create validation flow

### Purpose
Validate and create a reservation request while preventing invalid season/policy submissions and capacity over-allocation.

### Sequence
1. **Caller -> Reservation API (Create)**
   - **Inputs:** idempotency key, requester identity/context, desired slot identifier (or time+facility tuple), party size, participant details (as required), optional notes, correlation/request ID.
   - **Outputs:** accepted create command envelope.
2. **Reservation API -> SeasonService**
   - **Inputs:** facility and slot date/time.
   - **Outputs:** season validity and operating constraints.
3. **Decision: SeasonService validation fails**
   - If invalid, return **create rejected: season invalid** with machine-readable reason code.
4. **Reservation API -> BookingPolicyService**
   - **Inputs:** requester context, party size, booking timing, slot context.
   - **Outputs:** policy pass/fail + policy diagnostics.
5. **Decision: BookingPolicyService validation fails**
   - If policy check fails, return **create rejected: policy fail** with reason code(s).
6. **Reservation API -> AvailabilityService slot-inventory component (capacity check + provisional hold/update intent)**
   - **Inputs:** slot identifier, requested party size, idempotency key, expected slot version (if provided).
   - **Outputs:** capacity decision (fits/conflict), resulting slot version or conflict details.
7. **Decision: slot capacity conflict**
   - If capacity is insufficient or concurrent update invalidates request, return **create rejected: capacity conflict**.
8. **Reservation API -> ReservationService persistence component**
   - **Inputs:** validated reservation payload, allocation result, idempotency key.
   - **Outputs:** reservation record identifier, status, timestamps, version.
9. **Reservation API -> Caller**
   - **Outputs:** created reservation summary, status, and references needed for future update/cancel.

### Idempotency and concurrency note
- The create flow should treat the **idempotency key** as the deduplication key for retried create requests.
- Slot capacity updates should enforce optimistic concurrency (for example, slot version checks) or equivalent atomic guard so that only one competing allocation succeeds for the same remaining capacity.

## 3) Reservation update/cancel capacity adjustment flow

### Purpose
Process reservation changes (modify party size/slot) or cancellation, with correct capacity release/reallocation under concurrent activity.

### Sequence
1. **Caller -> Reservation API (Update or Cancel)**
   - **Inputs:** reservation identifier, operation type (update/cancel), requested new slot and/or new party size (update only), idempotency key, requester context, correlation/request ID.
   - **Outputs:** accepted command envelope.
2. **Reservation API -> ReservationService persistence component**
   - **Inputs:** reservation identifier.
   - **Outputs:** current reservation state, current slot allocation, reservation version/status from ReservationService.
3. **Reservation API -> SeasonService (update only when slot/date changes)**
   - **Inputs:** target slot date/time.
   - **Outputs:** season validity from SeasonService.
4. **Decision: SeasonService validation fails**
   - For invalid target season/date on update, return **update rejected: season invalid**.
5. **Reservation API -> BookingPolicyService (update only as needed)**
   - **Inputs:** requester context, current vs target slot/time, current vs target party size.
   - **Outputs:** policy pass/fail with diagnostics from BookingPolicyService.
6. **Decision: BookingPolicyService validation fails**
   - If failed, return **update rejected: policy fail**.
7. **Reservation API -> AvailabilityService slot-inventory component (capacity adjustment transaction intent)**
   - **Inputs:**
     - **Cancel:** current slot, current party size, release intent, idempotency key.
     - **Update (same slot, size change):** delta party size and expected slot version.
     - **Update (slot change):** release old slot capacity + acquire target slot capacity with concurrency guards.
   - **Outputs:** adjusted capacity state(s), conflict indicator(s), updated slot version(s).
8. **Decision: slot capacity conflict**
   - If target slot cannot accommodate update (or concurrent mutation detected), return **update rejected: capacity conflict**.
9. **Reservation API -> ReservationService persistence component**
   - **Inputs:** updated or canceled reservation state, capacity adjustment receipt(s), idempotency key.
   - **Outputs:** persisted reservation status/version in ReservationService.
10. **Reservation API -> Caller**
   - **Outputs:** final reservation state (updated/canceled), effective slot and party size, version and audit timestamps.

### Idempotency and concurrency note
- Update/cancel operations should be idempotent per `(reservation identifier, idempotency key, operation type)` to safely handle retries.
- Capacity release/acquire for slot changes should execute with atomicity or compensating safeguards to avoid lost capacity or double-booking under concurrent requests.
