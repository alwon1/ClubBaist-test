# Area D: Player Scoring – Design Questions

> **Answer after Areas F and E.**

These decisions shape the player scoring system: `RecordScore`, `ScoreConsole`, `MyScoreSubmissions`, `ScoreConfirmation`.

**Related review files:** `.review/area-d-gap.md`, `.review/area-d-design.md`, `.review/area-d-ui.md`, `.review/area-d-priority.md`

---

## Question 18 – WHS Handicap Index calculation

The **WHS (World Handicap System) Handicap Index calculation** is entirely missing from the scoring system. Currently `GolfRound` records are stored but no handicap differential is computed and no Handicap Index is maintained per member.

Is Handicap Index calculation **in scope for this phase**?

- **Option A – Full WHS implementation (best-8-of-last-20):** Calculate a score differential for each round and maintain a proper WHS Handicap Index using the best 8 differentials from the last 20 rounds. Requires additional columns on `GolfRound` (differential, adjusted gross score) and a `HandicapIndex` field on `MemberShipInfo`.
- **Option B – Per-round differential only (no full index):** Calculate and store the course handicap differential per round, but do not implement the multi-round averaging/best-of logic. Provides a building block for Option A later.
- **Option C – Defer entirely:** Handicap calculation is out of scope for this phase; scoring remains a pure data entry function.

**Your answer:**
<!-- e.g. "Option C – defer handicap calculation entirely for now" -->

---

## Question 19 – External/away course scoring

Should scoring for **external (away) courses** be supported in this phase?

Currently `GolfRound` is tied to a `TeeTimeBooking`, which in turn references a slot in the club's own season tee sheet. This means a member who played at a different course cannot record that round.

- **Option A – Support external course scoring:** Allow `GolfRound` records without a `TeeTimeBookingId` (make it nullable); add a free-text or lookup field for the external course name/rating/slope.
- **Option B – Defer external courses:** Out of scope for this phase; scoring is restricted to rounds played at the club. External course support is needed for a proper WHS implementation (Option A in Q18) but can be deferred if handicaps are also deferred.

**Your answer:**
<!-- e.g. "Option B – defer external courses; in-club scoring only for now" -->
