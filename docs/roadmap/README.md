# Roadmap

This directory is the **plan layer** for Nexora. It answers *where are we going* and *what does
each phase deliver*. It does **not** track day-to-day execution — that lives in
[`../analysis/tasks/`](../analysis/tasks/).

## Files

| File | Purpose |
|------|---------|
| [`current.md`](current.md) | **Start here.** The single sync point: active phase, next phase entry criteria, pending ADRs. |
| [`phases/README.md`](phases/README.md) | Index of all phase files. |
| `phases/phase-*.md` | One file per phase — scope, exit bar, milestones, ADR ledger, downstream consumers. |

## Philosophy

1. **Plan vs. execution are separate.** Phase files describe intent and are ADR-gated. Tasks
   describe current work and change daily. Never conflate them.
2. **One sync point.** Only `current.md` is guaranteed fresh for "where are we right now?".
   Update it in the same commit as any phase transition.
3. **`Done` requires maintainer review.** See the status vocabulary in
   [ADR-015](../decisions/0015-roadmap-structure.md) — contributors may mark tasks
   `In Review`; only the maintainer promotes to `Done`.

## Conventions

- **Phase IDs:** `phase-1-foundation`, `phase-1.5-bridge`, `phase-2-enterprise`,
  `phase-2.5-hr`, `phase-3a-ngo`, `phase-3b-education`, `phase-4-extensions`.
- **Milestone IDs:** `A`, `B`, `C` within a phase (e.g. `phase-2 / Milestone B`).
- **Task IDs:** `T-NNN` globally unique; task files live under
  `docs/analysis/tasks/phase-X/T-NNN.md`.
- **ADR ledger:** each phase file lists the ADRs it introduces, consumes, or supersedes.

## Changing the roadmap

- Scope or exit-bar changes to an existing phase → new ADR referencing the phase.
- Adding / removing a phase → new ADR.
- Task-level changes → edit the task file in `docs/analysis/tasks/`, **no ADR needed**.
- Any phase transition (e.g., 1.5 → 2 becoming active) → update `current.md` in the same commit.

## Related

- [Decisions (ADRs)](../decisions/) — the "why" behind the plan.
- [Standards](../standards/) — the "how" that every phase follows.
- [Analysis / Tasks](../analysis/tasks/) — the "what are we doing this week" execution layer.
