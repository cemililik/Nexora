---
name: conduct-business-review
description: Run the five-role Nexora business review (Chronicler, Plan-diff, Learning, Adjuster, Pathfinder) and produce a merged review document under docs/analysis/reviews/business-reviews/. Use when the user says "business review", "roadmap retro", "phase retro", or at any phase transition.
---

# Conduct Business Review

Run the five-role review defined in
`docs/analysis/reviews/business-reviews/master-plan.md`, then merge the passes into a
single authoritative review file.

## When to use

- At every phase transition (e.g. Phase 1.5 → Phase 2).
- Ad hoc when the maintainer signals a strategy shift.
- Before any ADR that changes tier classification or phase scope.

## When NOT to use

- PR-level reviews — use `perform-code-review`.
- Security audits — use `perform-security-review`.

## Inputs

- Period under review (start date → end date).
- Active phase at start and at end.
- Optional: triggering event (phase close, strategy pivot).

## Procedure

### Step 1 — Run the five passes independently

Each pass should be authored by a different contributor where possible. Each produces
one section of the merged file.

1. **Chronicler** — What actually happened? Factual narrative sourced from merged PRs,
   ADR promotions, task status transitions, `current.md` history. Past tense, no
   recommendations. Every claim cites a PR / task / ADR.
2. **Plan-diff** — What did the plan say vs. what shipped? Tabular variance report
   (Milestone → planned / actual / variance note). Calls out silent deferrals and
   scope additions without ADRs.
3. **Learning** — What did we learn? Reusable lessons, each with claim + evidence +
   proposed follow-up. May propose new standards docs or ADRs.
4. **Adjuster** — What must change in the plan? Concrete diffs against phase files,
   task priorities, or `current.md`. Scope changes → ADR required (drafts the ADR;
   maintainer promotes).
5. **Pathfinder** — What's next beyond the current horizon? One phase past active +
   next. Speculative, tagged `radar:`, not directly actionable.

### Step 2 — Merge

Produce `docs/analysis/reviews/business-reviews/YYYY-MM-DD-<slug>.md` with sections in
this fixed order:

1. Context (period, active phase at start/end, trigger).
2. Chronicler narrative.
3. Plan-diff table.
4. Learning list.
5. Adjuster proposals (each with target file, diff summary, ADR-required Y/N).
6. Pathfinder radar (tagged items).
7. Maintainer sign-off block (date, approver, ADRs promoted, follow-up tasks opened).

### Step 3 — Route follow-ups

- ADR proposals → file in `docs/decisions/` as Proposed (skill: `write-adr`).
- Task adjustments → open or transition `T-NNN` files (skill: `start-task`).
- Phase file edits → land in same commit as the merge file.
- `current.md` updates → land in same commit (ADR-015 sync-point rule).

## Outputs

- One dated merged review file under `business-reviews/`.
- (Optional) new ADRs, updated phase files, updated tasks, updated `current.md`.

## Related standards

- [Master plan](../../../docs/analysis/reviews/business-reviews/master-plan.md)
- [ADR-015 — Roadmap Structure](../../../docs/decisions/ADR-015-roadmap-structure.md)
- [ADR-016 — Module Tier Classification](../../../docs/decisions/ADR-016-module-tier-classification.md)
- `docs/roadmap/current.md` — the one sync point you must keep honest.
