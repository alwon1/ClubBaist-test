# Analysis & Design Documentation

## Structure
- `00-overview/`: assumptions, glossary, cross-cutting notes.
- `10-domain-membership-applications/`: first detailed domain slice.
  - `01-actors.md`
  - `02-use-case-catalog.md`
  - `03-use-cases/`
  - `04-ssds/`
  - `05-components/`
  - `06-class-model/`
  - `07-services/`
  - `08-reference/`
  - `09-testing/`
- `20-domain-tee-time-reservations/`: Phase 1 tee-time reservation domain slice.
  - `07-services/`

## Current Modeling Choice
Membership Applications is intentionally reduced to two use cases with alternative flows to keep scope compact and maintainable in early planning.

- `20-domain-tee-time-reservations/`: initial tee-time reservation backend planning slice.
  - `01-actors.md`
  - `02-use-case-catalog.md`
  - `03-use-cases/`
  - `04-architecture/`
