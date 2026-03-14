# Status Dictionary

## ApplicationStatus (Canonical)

| Code/Enum | Display Label | Meaning |
|---|---|---|
| `Submitted` | Submitted | New application successfully submitted and awaiting committee review. |
| `OnHold` | On Hold | Application deferred for later review/action. |
| `Waitlisted` | Waitlisted | Application deferred due to capacity/priority constraints. |
| `Accepted` | Accepted | Application approved; member creation workflow is triggered. |
| `Denied` | Denied | Application rejected; terminal status in v1. |

## Usage Rules
- All documentation and implementation references should use **`OnHold`** as the canonical enum token.
- UI text may display `On Hold` while persistence/logic uses `OnHold`.
- Transition rules are defined in `membership-application-class.md` and reused by service/use-case docs.
