# Player Scores – Scorekeeping Business Context

This document extracts and consolidates the Player Scores section of the Club BAIST Business Problem. It serves as the canonical business reference for all work in the `40-domain-player-scores` domain. Do not duplicate this content elsewhere; link back to this file instead.

---

## What the Club Wants

Club BAIST wants each member to **electronically provide their own scoring**, which must follow the guidelines of the **World Handicap System (WHS)**.

After completing a round of golf, a member can turn in their scorecard. Scores are processed and a resulting report is made available.

Scores from **Golf Canada-approved courses other than Club BAIST** can also be incorporated.

---

## Regulatory Framework

| Body | Role |
|------|------|
| **Golf Canada** | Authorized National body responsible for implementing and administering the Rules of Handicapping in Canada, in co-operation with provincial golf associations |
| **World Handicap System (WHS)** | The unified global system for measuring golfer performance and enabling fair play across ability levels |

> "The purpose of the World Handicap System (WHS) is to make the game of golf more enjoyable for golfers by providing a consistent means of measuring one's performance and progress and to enable golfers of differing abilities to compete, or play a casual round, with anyone else on a fair and equal basis."

### Referenced Documents

- `HandicappingReferenceGuide.pdf` — Handicapping Reference Guide
- `Golf-Canada-WHS-Rules-of-Handicap-–-ENG-Final.pdf` — Golf Canada WHS Rules of Handicap
- Golf Canada handicapping page
- USGA handicapping page

---

## Required Data for Each Golf Round

| Field | Notes |
|-------|-------|
| Date | Date the round was played |
| Golf Course | Name / location |
| Course Rating | Varies by tee colour and gender |
| Slope Rating | Varies by tee colour and gender |
| Hole-by-Hole Score | All 18 holes (or 9 for an abbreviated round) |
| Total Score | Sum of all hole scores |

---

## Club BAIST Scorecard

### Front Nine

| Hole | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | Out |
|------|--:|--:|--:|--:|--:|--:|--:|--:|--:|----:|
| Par | 4 | 5 | 3 | 4 | 4 | 4 | 3/4 | 5 | 4 | 36/37 |
| Red (yds) | 371 | 438 | 96 | 226 | 346 | 317 | 250 | 436 | 311 | 2,791 |
| White (yds) | 417 | 492 | 117 | 288 | 363 | 369 | 182 | 492 | 335 | 3,055 |
| Blue (yds) | 430 | 505 | 144 | 288 | 380 | 395 | 235 | 530 | 361 | 3,268 |

**Stroke Index (Men):** 1, 5, 17, 11, 9, 7, 15, 3, 13  
**Stroke Index (Women):** 5, 1, 17, 13, 11, 7, 15, 3, 9

### Back Nine

| Hole | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | In | Total |
|------|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|------:|
| Par | 4 | 4 | 3 | 5 | 4 | 4 | 3 | 4/5 | 4 | 35/36 | 71/73 |
| Red (yds) | 339 | 323 | 165 | 434 | 343 | 268 | 97 | 393 | 305 | 2,667 | 5,458 |
| White (yds) | 349 | 356 | 197 | 491 | 390 | 323 | 132 | 408 | 365 | 3,011 | 6,066 |
| Blue (yds) | 378 | 382 | 214 | 511 | 402 | 345 | 138 | 423 | 381 | 3,174 | 6,442 |

**Stroke Index (Men):** 12, 6, 16, 2, 10, 14, 18, 4, 8  
**Stroke Index (Women):** 8, 14, 16, 2, 6, 12, 18, 4, 10

---

## Course and Slope Ratings — Club BAIST

| Tee | Men Course Rating | Men Slope Rating | Women Course Rating | Women Slope Rating |
|-----|------------------:|-----------------:|--------------------:|-------------------:|
| Red | 66.2 | 116 | 71.0 | 125 |
| White | 68.8 | 123 | 75.0 | 133 |
| Blue | 70.9 | 127 | 76.6 | 138 |

The applicable course rating and slope rating for a submitted round are determined by the member's **tee colour selection** and their **gender on file**.

---

## Member Handicap Index Report

Fields shown on the report:

- Date
- Member Name
- Handicap Index
- Last 20 Average
- Best 8 Average
- Last 20 Round Scores

---

## Scope Notes (as of initial planning)

| Topic | Status |
|-------|--------|
| Club BAIST 18-hole rounds | **In scope — UC-PS-01** |
| External Golf Canada-approved course rounds | Deferred — future use case |
| 9-hole (abbreviated) rounds | Deferred — future use case |
| Handicap Index calculation | Deferred — separate use case |
| Scorecard attestation / marker signature | Deferred — out of current scope |
