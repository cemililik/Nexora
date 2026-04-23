# Tasks

One markdown file per task. File naming: `T-NNN.md`. Tasks are grouped by the phase that
owns them (`phase-1.5/`, `phase-2/`, …).

## Status vocabulary

Taken verbatim from [ADR-015](../../decisions/ADR-015-roadmap-structure.md). These are the
**only** allowed values:

| Status | Meaning | Who can set |
|--------|---------|-------------|
| `Not started` | Task file exists; no work yet | Anyone |
| `In Progress` | Actively being executed | Task owner |
| `In Review` | Execution complete; awaiting maintainer sign-off | Task owner |
| `Blocked` | Dependency or decision missing; owner must name blocker | Task owner |
| `Deferred` | Explicitly postponed to a named future phase | Maintainer |
| `Superseded` | Replaced by another task/ADR — link to replacement | Maintainer |
| `Done` | Maintainer has verified and closed | **Maintainer only** |

Contributors **must not** set `Done` or `Superseded`.

## ID scheme

- Task IDs are of the form `T-NNN` and are **globally unique** across the repo.
- `NNN` is a 3-digit zero-padded serial assigned at task creation. Allocate the next free
  ID across all phase folders.
- Do **not** reuse IDs after a task is superseded — link forward instead.

## Where each phase's tasks live

| Phase | Folder |
|-------|--------|
| Phase 1.5 — Bridge | [`phase-1.5/`](phase-1.5/) |
| Phase 2 — Enterprise Core | [`phase-2/`](phase-2/) (populated when Phase 2 starts) |
| Phase 2.5, 3a, 3b, 4 | folders created at each phase kickoff |

## Creating a new task

1. Copy [`TEMPLATE.md`](TEMPLATE.md) to `phase-X/T-NNN.md` using the next free ID.
2. Fill in the metadata block and Acceptance criteria.
3. Mark `Status: Not started` or `In Progress` if you are starting immediately.
4. Add a Status log entry for every status transition.

## Related

- [Task template](TEMPLATE.md)
- [ADR-015 — Roadmap Structure](../../decisions/ADR-015-roadmap-structure.md)
- [Roadmap phases](../../roadmap/phases/)
