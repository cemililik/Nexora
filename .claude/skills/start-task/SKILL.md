---
name: start-task
description: Begin execution on a roadmap task by creating (or transitioning) a T-NNN task file under docs/analysis/tasks/phase-X/ per ADR-015's status vocabulary. Use when the user says "start T-NNN", "pick up task", "open a task for X", or "I'm beginning work on <scope>".
---

# Start Task

Opens a new Nexora task or transitions an existing one into `In Progress`. Task files are
the execution layer (ADR-015); they live under `docs/analysis/tasks/phase-X/T-NNN.md` and
are distinct from phase files (the plan layer).

## When to use

- Beginning work on a scoped chunk of a phase (always create / transition a task first).
- Splitting a large phase-file scope item into multiple shippable tasks.
- Re-opening a `Blocked` task once the blocker clears.

## Inputs

- Phase ID (e.g. `phase-1.5`, `phase-2`).
- Task title (one line, imperative).
- Optional: existing task ID to transition (`T-012` etc.) instead of creating new.
- Owner (GitHub handle).

## Procedure

1. **Locate the owning phase file.** Confirm scope by reading
   `docs/roadmap/phases/phase-X-*.md`. Cite the exact section/milestone in the task.
2. **Allocate a `T-NNN` ID** (global, not per-phase). Run `ls
   docs/analysis/tasks/**/T-*.md | sort` to find the next free integer. Never reuse IDs.
3. **Copy the template.**
   ```bash
   cp docs/analysis/tasks/TEMPLATE.md \
      docs/analysis/tasks/phase-X/T-NNN-kebab-slug.md   # or just T-NNN.md
   ```
4. **Fill the metadata block.** `Status: In Progress` (or `Not started` if you are only
   filing, not starting). Owner, phase, milestone, created date.
5. **Write Acceptance criteria** by copying the matching bullets from the phase file's
   Acceptance criteria or Scope. Each criterion must be objectively verifiable.
6. **Add links.** PR placeholder (update after opening), related tasks, any plan doc.
7. **Append a Status log line** with today's date and the transition.
8. **Update `docs/roadmap/current.md`** only if this task changes the active phase or
   next-phase entry criteria (ADR-015 sync-point rule).

## Status vocabulary (ADR-015)

`Not started` · `In Progress` · `In Review` · `Blocked` · `Deferred` · `Superseded` ·
`Done` (maintainer-only).

Never set `Done` yourself.

## Outputs

- A committed `T-NNN.md` with filled-in metadata, acceptance criteria, and status log.
- (Optional) updated `current.md` if the sync-point rule applies.

## Related standards

- [ADR-015 — Roadmap Structure](../../../docs/decisions/ADR-015-roadmap-structure.md)
- [Tasks README](../../../docs/analysis/tasks/README.md)
- [Task template](../../../docs/analysis/tasks/TEMPLATE.md)
