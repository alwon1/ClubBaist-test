# Area F: Cross-Cutting Concerns – Design Questions (answer first)

> **⚠️ Answer these questions before any other area — decisions here affect all other areas.**

These cross-cutting decisions determine the naming conventions, database approach, and interface design that every other feature area depends on.

**Related review files:** `.review/area-f-gap.md`, `.review/area-f-design.md`, `.review/area-f-ui.md`, `.review/area-f-priority.md`

---

## Question 1 – "2-suffix" rename strategy

Should we do the **"2-suffix rename"** (`Domain2` → `Domain`, `Services2` → `Services`, `SeasonService2` → `SeasonService`, `IAppDbContext2` → `IAppDbContext`, etc.) as a **single up-front PR** before feature work begins, or **defer until after** features are done?

- **Option A – Rename first (single PR):** All feature branches get clean names, but requires rebase of any in-flight branches.
- **Option B – Defer rename:** Avoids rebase disruption now, but leaves naming noise throughout all feature work.

**Your answer:**
<!-- e.g. "Option A – rename first in a single PR" -->

---

## Question 2 – IAppDbContext2 interface

Should we **remove `IAppDbContext2` and inject `AppDbContext` directly**, or **keep the interface** and just update its misleading doc comment?

- **Option A – Remove interface, inject `AppDbContext` directly:** Simpler, less indirection. Touches ~38 files.
- **Option B – Keep the interface, update doc comment:** Preserves the seam for future testability, minimal change.

**Your answer:**
<!-- e.g. "Option B – keep the interface" -->

---

## Question 3 – Unique DB indexes / constraints

The DB currently lacks unique indexes on:
- `(TeeTimeSlotStart, BookingMemberId)` for **TeeTimeBooking**
- `(TeeTimeBookingId, MembershipId)` for **GolfRound**

Without these, concurrent requests under snapshot isolation could race past application-layer guards and insert duplicates.

Should we add these as a **standalone migration PR now**, or **fold them into the feature PRs** for Areas B and D?

- **Option A – Standalone migration PR now:** Ensures data integrity immediately, establishes migrations pattern early.
- **Option B – Fold into feature PRs B and D:** Keeps related changes together; migration lands with the feature.

**Your answer:**
<!-- e.g. "Option A – standalone migration PR now" -->

---

## Question 4 – EF Migrations vs EnsureCreated

Should **EF Migrations be introduced** (currently `EnsureCreatedAsync` is used in dev/test), or **stay on EnsureCreated** for this phase?

- **Option A – Introduce EF Migrations:** Proper change management for production deployments; required if we add the unique indexes above via a migration PR.
- **Option B – Stay on EnsureCreated:** Simpler for dev/test, acceptable if always deploying to a fresh database.

**Your answer:**
<!-- e.g. "Option A – introduce EF migrations" -->
