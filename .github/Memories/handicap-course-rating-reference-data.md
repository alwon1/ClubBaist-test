# Memory: Handicap course rating reference model location

## Metadata

- PatternId: memory-handicap-course-rating-model
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-22
- LastValidatedAt: 2026-04-22
- ValidationEvidence: Entity, DbSet, and seed data compile in solution build.

## Source Context

- Triggering task: Data modeling for course ratings and slope ratings by tee color and gender.
- Scope/system: ClubBaist.Domain2 scoring model
- Date/time: 2026-04-22

## Memory

- Key fact or decision: Course/slope ratings are modeled as reference data in CourseRating and seeded in AppDbContext.OnModelCreating.
- Why it matters: Handicap calculations can query authoritative values without persisting derived handicap data.

## Applicability

- When to reuse: Any service or query that needs course rating and slope rating by tee color and gender.
- Preconditions/limitations: Uses EnsureCreated/data seeding flow; no migration artifact generated in this task.

## Actionable Guidance

- Recommended future action: Query IAppDbContext2.CourseRatings with TeeColor + Gender as composite key.
- Related files/services/components: ClubBaist.Domain2/Entities/Scoring/CourseRating.cs, ClubBaist.Domain2/AppDbContext.cs, ClubBaist.Domain2/IAppDbContext2.cs