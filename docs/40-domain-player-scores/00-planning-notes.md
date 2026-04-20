# Plan: Score Recording Feature — Fully Dressed Use Case (Happy Path)

## In My Own Words — What Was Asked

Add functionality for recording golf scores for players. Rather than jumping straight to implementation, the approach is deliberate planning stages. The first stage is to produce a **fully dressed use case for the happy path** of score recording. Before doing that, the steps were:

1. Read and understand the business problem
2. Extract the scorekeeping-specific business context from the business problem into a **standalone reference document**
3. **Ask critical requirements questions** and get answers before writing anything
4. Write the fully dressed use case
5. **Present it for feedback** before treating it as final

There are **multiple explicit feedback points** built into this process — not one review at the end.

---

## Planning Approach (The Meta-Plan)

### Stage 0 — Explore & Extract (DONE)
- Read the business problem, existing use cases, and codebase structure
- Domain: Club BAIST is a private golf club (Alberta, 1996); the scoring system must comply with the **World Handicap System (WHS)** administered in Canada by **Golf Canada**
- Format reference: UC-TT-01 is the template for fully dressed use cases in this project

### Stage 1 — Create Scorekeeping Context Document (DONE)
- `docs/40-domain-player-scores/00-scorekeeping-business-context.md`

### Stage 2 — Draft Fully Dressed Use Case (DONE)
- `docs/40-domain-player-scores/03-use-cases/UC-PS-01-record-player-score.md`
- **Feedback Point #2**: Under review

### Stage 3 — Incorporate Feedback & Finalize (DONE)
- UC-PS-01 use case approved; scope narrowed to raw score entry only
- Key decisions: `List<uint?>` for hole scores, tee color as only round metadata, one-way navigation (no existing entity changes)

### Stage 4 — Design Documentation (IN PROGRESS)

**Scope:** MVP = entering and recording a player's raw golf score only. Nothing outside that is in scope. Stored per round: booking FK, member FK, tee color, 18 hole scores, submission timestamp, acting user.

**Execution order:** 19 steps with 8 review checkpoints. One commit per step; separate commits for corrections. See plan file for full table.

**Design decisions confirmed:**

| # | Decision |
|---|----------|
| D1 | Hole scores: `List<uint?>` on `GolfRound`, length 18. UI-side range validation (1–20) on blur and submit. |
| D2 | No changes to existing entities (`TeeTimeBooking`, `MemberShipInfo`, user tables). One-way navigation only. |
| D3 | Weather / course conditions deferred as UC-PS-06. `GolfRound` joinable via slot date — no future changes needed to `GolfRound`. |
| D4 | Course rating, slope rating, par not stored in MVP. Tee color stored as the future lookup key for UC-PS-03. |
| D5 | XML doc comments required on all new scoring domain classes and public members. Content sourced from design docs. |
| D6 | UC-PS-05 (9-hole rounds) removed entirely. UC-PS-06 (weather) added as deferred. |

**Feedback points added (Stage 4):**

| Point | Step | What |
|-------|------|------|
| #4 | Step 3 | Overall design overview |
| #5 | Step 4 | Site map diagram |
| #6 | Step 8 | All UI wireframes |
| #7 | Step 11 | Data class design + ER diagram |
| #8 | Step 12 | Class relationship diagram |
| #9 | Step 14 | ScoreService eligibility design |
| #10 | Step 17 | Full service design |
| #11 | Step 18 | Test plan |

---

## Requirements Decisions (confirmed via Q&A — two rounds)

| # | Question | Decision |
|---|----------|----------|
| Q1 | Who is the primary actor? | **Both** — Member self-service is the main flow; Clerk-assisted is alternate flow A5 (same pattern as UC-TT-01) |
| Q2 | Is WHS attestation required in this UC? | **Deferred / out of scope** — noted as a business rule but not enforced yet |
| Q3 | Is handicap calculation part of this UC? | **No — separate UC** — this UC only stores round data; handicap is a future use case |
| Q4 | Are external (non-Club-BAIST) courses in scope? | **Deferred — separate UC** — happy path covers Club BAIST rounds only |
| Q5 | 9-hole or 18-hole rounds? | **18-hole only** — abbreviated rounds deferred |
| Q6 | Who can a score be submitted for? | **Tied to completed tee time bookings only** — member selects from their past bookings |
| Q7 | Time-lock before submission allowed? | **Yes** — minimum duration after booking start time, scaled by player count |
| Q8 | Score validation bounds? | **WHS guidelines** — per hole: min 1, max 20; total = sum of 18 holes |

### Minimum Round Durations (fast-player baseline)

| Players in Booking | Minimum Time Before Score Can Be Submitted |
|--------------------|---------------------------------------------|
| 1 | 2 hours |
| 2 | 2 hours 30 minutes |
| 3 | 3 hours |
| 4 | 3 hours 30 minutes |

---

## Feedback Points

| Point | When | What |
|-------|------|------|
| #1 | Before writing (DONE) | Critical requirements Q&A (5 questions) |
| #1b | Before writing (DONE) | Additional requirements Q&A (booking tie-in, time-lock, score bounds) |
| #2 | After drafting (AWAITING) | Review of the fully dressed use case UC-PS-01 |
| #3 | After feedback incorporated (FUTURE) | Final sign-off before implementation planning |

---

## Files Created / Updated

| File | Purpose |
|------|---------|
| `docs/40-domain-player-scores/00-scorekeeping-business-context.md` | Canonical scorekeeping business context |
| `docs/40-domain-player-scores/02-use-case-catalog.md` | UC-PS catalog (UC-PS-05 removed; UC-PS-06 added) |
| `docs/40-domain-player-scores/03-use-cases/UC-PS-01-record-player-score.md` | Fully dressed use case (happy path) |
| `docs/40-domain-player-scores/00-deferred-planning-notes.md` | Design decisions for future use cases |
| `docs/40-domain-player-scores/00-planning-notes.md` | This file |
| `docs/40-domain-player-scores/01-overview.md` | Overall design direction overview (Stage 4) |
| `docs/40-domain-player-scores/04-ui-design.md` | UI wireframes and flow diagrams (Stage 4) |
| `docs/40-domain-player-scores/05-class-model/tee-color-enum.md` | TeeColor enum design (Stage 4) |
| `docs/40-domain-player-scores/05-class-model/golf-round-class.md` | GolfRound entity design with ER + class diagrams (Stage 4) |
| `docs/40-domain-player-scores/06-services/score-service.md` | ScoreService design (Stage 4) |
| `docs/40-domain-player-scores/07-testing/score-service-test-plan.md` | Test plan (Stage 4) |
