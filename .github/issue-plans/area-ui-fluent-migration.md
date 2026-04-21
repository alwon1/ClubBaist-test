# Area UI: Fluent UI Blazor Migration – Design Questions

> **Answer last** — UI migration strategy depends on feature-area decisions above.

These decisions govern the migration from Bootstrap + QuickGrid to **Microsoft.FluentUI.AspNetCore.Components v4** (Fluent UI Blazor).

---

## Question 20 – Migration strategy

How should the **Fluent UI Blazor migration** be structured?

- **Option A – Migrate all pages in one large PR:** Single branch, single merge. Fastest if done in one session but creates a very large diff and a long-lived feature branch that is hard to review and conflicts with feature work.
- **Option B – Migrate one area at a time on separate branches (independently mergeable):** Each area (A, B, C, D, admin pages) gets its own UI-migration PR. Allows incremental review and merge; feature work and UI migration can proceed in parallel on different pages.
- **Option C – Page-by-page migration interleaved with feature work:** Each feature PR also migrates the pages it touches. No separate "UI migration" work stream; keeps related changes together but mixes feature logic and component changes in the same diff.

**Your answer:**
<!-- e.g. "Option B – migrate one area at a time, independently mergeable" -->

---

## Question 21 – Bootstrap coexistence

Should the **Bootstrap dependency be removed entirely**, or can **Fluent UI and Bootstrap coexist** while pages are migrated incrementally?

- **Option A – Remove Bootstrap entirely (single cut-over):** Clean result but requires all pages to be migrated before Bootstrap can be removed. Only viable if Option A (big-bang) was chosen in Q20.
- **Option B – Allow coexistence during migration:** Bootstrap stays until all pages have been migrated to Fluent UI; then removed in a cleanup PR. Required for incremental migration (Options B or C in Q20).

**Your answer:**
<!-- e.g. "Option B – allow coexistence; remove Bootstrap in a cleanup PR at the end" -->

---

## Question 22 – QuickGrid replacement

Should all existing **`<QuickGrid>`** usages be replaced with **Fluent UI `<FluentDataGrid>`**, or should QuickGrid be kept where it is already working?

- **Option A – Replace all QuickGrid with FluentDataGrid:** Consistent component library; requires refactoring all list views that use QuickGrid (UserManagement, MyReservations, ScoreConsole, etc.).
- **Option B – Keep QuickGrid where already working:** Less churn; only new list views use FluentDataGrid. Bootstrap styling is removed but QuickGrid remains. Mixed component story but lower migration risk.

**Your answer:**
<!-- e.g. "Option A – replace all QuickGrid with FluentDataGrid for consistency" -->
