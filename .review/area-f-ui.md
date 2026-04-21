# Area F: Cross-Cutting — UI Analysis & Fluent UI Redesign

## Current UI Issues

### NavMenu
1. **"My Standing Requests" shown to all Members** — the nav item at `/teetimes/standing/my` is rendered for `AppRoles.Member` but should only appear for users with the `standing-tee-time.book` claim (Shareholders). Non-shareholder members see a link they cannot use meaningfully.
2. **Staff Console and Score Console require Admin** — clerks and pro shop staff must log in as admins to access these surfaces. No Clerk or ProShopStaff role is reflected in the nav.
3. **No "Record Score" nav link** — members must navigate via My Scores or the Home dashboard card; there is no direct nav entry for score submission.
4. **Register link commented out** — `Account/Register` is commented out with no explanation. New accounts are created only via application approval or admin, but this is invisible to users.
5. **Username display** — the nav shows `@context.User.Identity?.Name` (the email address) rather than a friendly display name (`FirstName LastName`).
6. **No nav grouping** — all links are a flat list with no section headers or grouping (e.g., "Golf", "Account", "Admin"). As more features are added this will become unnavigable.

### Home Page
1. **Static Bootstrap card grid** — the home page is a generic tile layout with no personalization. A Gold member and a Bronze member see the same cards despite having different access.
2. **Standing Tee Time card shown to all Members** — same problem as NavMenu; all members see the Standing Tee Time card regardless of Shareholder status.
3. **No greeting or account summary** — members see no indication of their membership level, next booking, or score history on landing.
4. **Score submission not on home** — "Record Score" is not on the home page, only accessible from My Scores nav link.
5. **No responsive design considerations** — Bootstrap grid (`col-md-4`) is used but no mobile-first treatment visible.

### Global Patterns
1. **No toast/snackbar notifications** — success and error feedback is per-page (often just text near the submit button), with no global notification system.
2. **No loading indicators** — async operations (booking, score submit) have no spinner or disabled-state feedback visible in the shared layout.
3. **No global error boundary beyond `/Error`** — component-level errors are not caught; an unhandled exception in a component crashes the whole circuit.
4. **Bootstrap icons only** — `bi bi-*` icon classes; no icon system that aligns with Fluent UI.
5. **Bootstrap CSS + Blazor default styles** — no design system consistency; mixing Bootstrap utility classes and custom CSS will create maintenance burden as the UI grows.

---

## Fluent UI Blazor Redesign Proposal

### Application Shell & Navigation

**Recommended shell components:** `<FluentLayout>`, `<FluentHeader>`, `<FluentNavMenu>`, `<FluentNavGroup>`, `<FluentNavLink>`, `<FluentBodyContent>`, `<FluentFooter>`

**Layout sketch:**
```
┌──────────────────────────────────────────────────────┐
│  FluentHeader: [Club BAIST logo]   [Member Name ▾]   │
├─────────────┬────────────────────────────────────────┤
│ FluentNav   │                                        │
│ Menu        │   FluentBodyContent (page content)     │
│ (collapsible│                                        │
│  sidebar)   │                                        │
├─────────────┴────────────────────────────────────────┤
│  FluentFooter: Club BAIST © 2024                     │
└──────────────────────────────────────────────────────┘
```

The sidebar uses `<FluentNavGroup>` to group related links, collapses on mobile, and hides items based on role/claim checks — same logic as today but structured.

---

### Role-Based Navigation Structure

| Role / Claim | Nav Groups & Links |
|---|---|
| **Guest** | Apply for Membership, Login |
| **Member (all)** | Golf: Tee Times / My Reservations / Record Score / My Scores; Account: Profile / Logout |
| **Member + `standing-tee-time.book` claim** | Golf: (above) + Standing Tee Time Request / My Standing Requests |
| **MembershipCommittee** | Membership: Application Inbox; (+ Member links if also a Member) |
| **Clerk** (new role) | Staff: Score Console / Staff Check-In; Account: Logout |
| **ProShopStaff** (new role) | Staff: Staff Console (Tee Sheet) / Manage Special Events; Account: Logout |
| **Admin** | All of the above + Admin: User Management / Member Management / Seasons / Roles & Claims |

---

### Dashboard / Home per Role

Replace the static Bootstrap card grid with a `<FluentGrid>` of `<FluentCard>` components that render conditionally per role. Each card is a quick-action tile with an icon, title, description, and a `<FluentButton>` call-to-action.

**Member dashboard cards:**
- Next Tee Time (shows upcoming reservation or "Book Now" if none)
- Record Score (if eligible bookings exist — badge shows count)
- My Scores (last submitted round summary)
- Standing Request status (Shareholders only)

**Staff dashboard cards:**
- Today's Tee Sheet (quick link to StaffConsole with today pre-selected)
- Pending Scores (count badge)

**Committee dashboard cards:**
- Pending Applications (count badge)

**Admin dashboard cards:**
- All staff cards + system health summary

---

### Global UI Patterns

| Pattern | Current | Proposed |
|---|---|---|
| Success/error feedback | Per-page text near submit | `<FluentToastProvider>` + `<FluentToast>` shown globally |
| Confirmation dialogs | Inline `@if` show/hide | `<FluentDialogProvider>` + `<FluentDialog>` with confirm/cancel |
| Loading states | None | `<FluentProgressRing>` overlay or `<FluentSkeleton>` for data grids |
| Empty states | Varies | Consistent `<FluentCard>` with icon + message + action button |
| Error boundary | Global /Error page | Per-component `<ErrorBoundary>` + global Fluent error toast |
| Icons | Bootstrap icons (`bi-*`) | Fluent UI icons via `<FluentIcon>` (Microsoft.FluentUI.AspNetCore.Components.Icons) |
| Data tables | QuickGrid (`<QuickGrid>`) | `<FluentDataGrid>` (supports sorting, pagination, virtualization) |

---

## Notes

- The current NavMenu is a standard Blazor sidebar wired to Bootstrap. Migrating to Fluent UI shell requires replacing `MainLayout.razor` and `NavMenu.razor` — this is the first change in any UI rewrite and unblocks all subsequent page rewrites.
- `<FluentToastProvider>` and `<FluentDialogProvider>` should be added to `Routes.razor` or `App.razor` once, making them available application-wide.
- The `AuthorizeView` pattern used today maps directly to Fluent UI navigation — `<FluentNavLink>` supports conditional rendering the same way.
- Fluent UI Blazor package: `Microsoft.FluentUI.AspNetCore.Components` (available on NuGet). Requires adding `builder.Services.AddFluentUIComponents()` and the CSS/JS imports.
