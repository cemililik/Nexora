# ADR-015: Roadmap Structure and Phase Model

## Status
Accepted

## Date
2026-04-22

## Context

Nexora's planning so far has lived in a single 1,211-line `docs/roadmap/ROADMAP.md`. As the
product pivoted toward an enterprise/SMB core with vertical Editions (NGO, Education) layered on
top, the monolithic roadmap became hard to reason about:

- Plan (intent, scope, exit bar) and execution (in-flight tasks, daily progress) were interleaved
  and co-edited, so neither view was reliable.
- Status words (`[ ]`, `[x]`, "deferred", "planned") had no shared definition; any author could
  mark any item "done" without a review gate.
- There was no single "where are we right now?" page, so every consumer had to re-derive the
  current phase from section markers and dates.
- Cross-phase work (Tier-2 enterprise core vs. Tier-3 verticals vs. extensions) could not be
  scheduled or reasoned about independently because they shared one document.

We need a roadmap that separates **plan** (stable, changes via ADR) from **execution** (volatile,
changes continuously), enforces a status vocabulary, and gives one clear sync point.

## Decision drivers

- Plan vs. execution must be separable — editing one must not churn the other.
- A single file must answer "what are we doing right now?" in ≤ 30 seconds.
- Task/milestone/phase hierarchy must be explicit so cross-phase parallelism is safe.
- Status promotion rules must be consistent: who can mark a task `Done`, and when.
- Maintainer authority over scope and sign-off must be mechanically enforced.

## Considered options

### Option A — Keep monolithic ROADMAP.md, add discipline
Add status conventions and review gates to the existing file. No structural change.
- (+) Zero migration cost.
- (−) The underlying plan/execution mixing is not fixed; the file keeps growing.
- (−) No natural place to put Editions / NMP track — they pollute the same file.

### Option B — Phase-per-file + `current.md` sync (Umbrix pattern)
Split the roadmap into `docs/roadmap/phases/phase-*.md` (one file per phase), add `current.md`
as the only "where are we" sync point, move execution state into `docs/analysis/tasks/` (task
files separate from the plan), and adopt a fixed status vocabulary with `Done` requiring
maintainer review.
- (+) Plan and execution live in different trees and are independently editable.
- (+) `current.md` is the single source of truth consumers check first.
- (+) Each phase file is self-contained (scope, exit bar, milestones, ADR ledger) — smaller
  diffs, clearer reviews.
- (−) Migration cost; consumers must re-learn where to look.

### Option C — Issue-tracker-only
Deprecate markdown roadmap; use GitHub Projects / Linear as source of truth.
- (+) Live, queryable, granular.
- (−) No offline / git-history reasoning; loses ADR-grade permanence.
- (−) Fragments architectural intent across external systems.

## Decision outcome

**Chosen: Option B.** The roadmap becomes a three-layer structure:

1. **Phase files** (`docs/roadmap/phases/phase-*.md`) — the plan. One file per phase. Each
   contains: Exit bar, Scope, Out of scope, Milestones, Acceptance criteria, ADR ledger, and
   "Informs" (downstream consumers). Phase files change via ADR when scope shifts.
2. **`docs/roadmap/current.md`** — the one sync point. Names the active phase, the next phase,
   its entry criteria, and any pending ADRs awaiting maintainer promotion. Updated whenever the
   active phase changes.
3. **Task files** (`docs/analysis/tasks/phase-*/T-NNN.md`) — the execution layer. One file per
   task, following a standard template. Task status is authoritative here, not in the phase file.

### Status vocabulary (exhaustive)

| Status | Meaning | Who can set |
|--------|---------|-------------|
| `Proposed` | Drafted; awaiting maintainer review | Any agent / contributor |
| `In Progress` | Actively being executed | Task owner |
| `In Review` | Execution complete; awaiting maintainer sign-off | Task owner |
| `Done` | Maintainer has verified and closed | **Maintainer only** |
| `Blocked` | Dependency or decision missing; owner must name blocker | Task owner |
| `Deferred` | Explicitly postponed to a named future phase | Maintainer |
| `Superseded` | Replaced by another task/ADR — link to replacement | Maintainer |

### Hierarchy

- **Phase** (`phase-2-enterprise.md`) → groups milestones by strategic release.
- **Milestone** (A/B/C within a phase) → groups tasks by shippable unit.
- **Task** (`T-NNN.md` under `analysis/tasks/phase-X/`) → smallest unit of execution.

### Sync point rule

Any change to the active phase, the next-phase entry criteria, or a pending-ADR list **must
also update `current.md` in the same commit**. The phase/task files are not required reading
for daily consumers — `current.md` is.

## Consequences

### Positive
- Consumers get a single-page answer to "what are we doing now?" → `current.md`.
- Plan changes (ADR-gated) and execution status changes (continuous) no longer collide in diffs.
- Phase files become independently reviewable; cross-phase parallelism is safe.
- `Done` is mechanically gated by maintainer review, closing the "silent completion" gap.
- Task template standardization makes handoffs and business reviews consistent.

### Negative
- Migration cost: the legacy `docs/roadmap/ROADMAP.md` is split across ~7 phase files and ~20
  archived plan documents. One-time effort (Phase 2 Agent A).
- Two-step workflow: authors must update both a task file and `current.md` when the active phase
  changes. Partial discipline drift is possible; mitigate via PR checklist.
- More files → more places to keep cross-links correct. Mitigate via link-check in CI.

### Neutral
- Existing ADRs are unchanged in content; only numbering format shifts to `NNNN-kebab.md`.
- External consumers who bookmarked `ROADMAP.md` get a redirect via `_archive/roadmap-legacy.md`.

## Implementation notes

- Phase file template and task template are written by Phase 2 Agent A.
- Migration from legacy ROADMAP.md is planned in `docs/_archive/migration-notes.md`.
- `current.md` is seeded in Phase 1 (this change set); kept live thereafter.

## References

- `docs/_archive/migration-notes.md` — inventory and execution plan
- ADR-016 — Module Tier Classification (consumer of the phase model)
- Umbrix OS — prior art for phase-per-file + current.md pattern
