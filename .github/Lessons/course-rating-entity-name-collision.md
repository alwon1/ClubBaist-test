# Lesson: Avoid type-member name collisions in C# entities

## Metadata

- PatternId: lesson-csharp-entity-name-collision
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-22
- LastValidatedAt: 2026-04-22
- ValidationEvidence: Build failed with CS0102/CS0229, then passed after moving enums to namespace scope.

## Task Context

- Triggering task: Add CourseRating reference data entity and seed data for handicap support.
- Date/time: 2026-04-22
- Impacted area: ClubBaist.Domain2 scoring entities

## Mistake

- What went wrong: Defined nested enums named TeeColor and Gender inside class CourseRating while also creating properties with the same names.
- Expected behavior: Entity compiles with strongly typed enum properties.
- Actual behavior: Compiler errors CS0102 and CS0229 due to ambiguous/conflicting member names.

## Root Cause Analysis

- Primary cause: C# does not allow a member/type name collision inside the same enclosing type.
- Contributing factors: Attempt to keep enums local to entity class for minimal scope.
- Detection gap: No pre-build check for naming collisions before first compile.

## Resolution

- Fix implemented: Moved TeeColor and Gender enums to namespace scope and kept property names on CourseRating.
- Why this fix works: Removes symbol ambiguity while preserving domain semantics and EF mapping.
- Verification performed: dotnet build ClubBaist.slnx succeeded.

## Preventive Actions

- Guardrails added: Prefer namespace-level enums when property names must match domain language.
- Tests/checks added: None.
- Process updates: Compile immediately after introducing new domain enums/properties.

## Reuse Guidance

- How to apply this lesson in future tasks: When adding entity enums, check that enum type names do not duplicate property names inside the same class.