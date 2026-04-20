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

### Stage 3 — Incorporate Feedback & Finalize (PENDING)
- **Feedback Point #3**: Final sign-off before implementation planning

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

## Files Created

| File | Purpose |
|------|---------|
| `docs/40-domain-player-scores/00-scorekeeping-business-context.md` | Canonical scorekeeping business context |
| `docs/40-domain-player-scores/02-use-case-catalog.md` | UC-PS catalog with deferred items |
| `docs/40-domain-player-scores/03-use-cases/UC-PS-01-record-player-score.md` | Fully dressed use case (happy path) |
| `docs/40-domain-player-scores/00-planning-notes.md` | This file |
