# Player Scores – Overall Design Overview

This document gives a high-level picture of the three design areas — pages, data classes, and services — before detailed design begins. Its purpose is to establish shared direction so that detailed work in each area is coherent.

---

## MVP Scope Reminder

MVP = entering and recording a player's raw golf score tied to a completed tee time booking. Nothing else.

**Stored per round:** booking reference, tee color (Red / White / Blue), 18 raw hole scores, submission timestamp, acting user (audit only).

**Not stored:** course rating, slope rating, par, total score. These will be computable when handicap features are added later.

---

## Pages (UI)

Three new pages for the score submission workflow, plus a navigation entry:

| Page | Actor | Purpose |
|------|-------|---------|
| My Score Submissions (list) | Member | Entry point — shows eligible bookings and past submitted rounds |
| Score Entry Form | Member / Clerk | Select booking, choose tee color, enter 18 hole scores |
| Submission Confirmation | Member / Clerk | Confirms successful submission with date, tee color, and score summary |

Staff navigation: Admin / Clerk accesses the same workflow via a member search, same as the tee time clerk console pattern.

---

## Data Classes

Three new types in the scoring domain:

```
TeeColor (enum)
  Red | White | Blue

GolfRound (entity)
  Id                    int, PK, auto-generated
  TeeTimeBookingId      int, FK → TeeTimeBooking (one-way, no back-navigation)
  TeeTimeBooking        TeeTimeBooking (navigation property — one-way)
  MembershipId          int, FK → MemberShipInfo
  Member                MemberShipInfo (navigation property — required for EF Core mapping)
  TeeColor              TeeColor (required)
  Scores                List<uint?>, length 18, initialized on construction
  SubmittedAt           DateTime (local server time, DateTimeKind.Unspecified)
  ActingUserId          string (ASP.NET Identity user ID — member or clerk)
```

No changes to any existing entities (`TeeTimeBooking`, `MemberShipInfo`, identity tables).

A composite unique index on `GolfRound(TeeTimeBookingId, MembershipId)` enforces one score per booking per member at the database level.

---

## Services

One new service:

```
ScoreService
  GetEligibleBookingsAsync(memberId)
    → returns bookings past the time-lock window with no existing GolfRound

  SubmitRoundAsync(request, actingUserId)
    → validates, stores GolfRound in a snapshot-isolation transaction

  GetRoundsByMemberAsync(memberId)
    → returns all GolfRound records for a member (for future reporting)
```

`ScoreService` depends on `IAppDbContext2` (same abstraction used by `BookingService`) — no new infrastructure needed.

---

## Compatibility with Future Features

| Future feature | How MVP design accommodates it |
|----------------|-------------------------------|
| UC-PS-03 Handicap calculation | Tee color stored → course/slope rating lookup at calculation time. No schema changes to `GolfRound`. |
| UC-PS-06 Weather / conditions | `CourseCondition` (future entity) joins via `TeeTimeBooking.TeeTimeSlotStart` date. No changes to `GolfRound`. |
| UC-PS-02 External courses | `GolfRound` extensible via discriminator or separate entity. Tee color field may become nullable for external rounds. |

---

## XML Doc Comments

All new classes, enums, and public members in this domain require XML doc comments (`/// <summary>`). Content is sourced from this design documentation. This is the first domain in the project to adopt this standard — it applies at implementation time.
