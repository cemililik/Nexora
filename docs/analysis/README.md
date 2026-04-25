# Analysis

This directory is the **execution layer** of the Nexora roadmap. It tracks day-to-day work
(tasks) and the reviews that feed product/engineering decisions. It intentionally lives
apart from [`../roadmap/`](../roadmap/), which is the **plan layer** (ADR-gated, slow-moving).

## Structure

| Path | Purpose |
|------|---------|
| [`tasks/`](tasks/) | One file per task (`T-NNN.md`), grouped by phase. Daily execution state. |
| [`reviews/`](reviews/) | Business / code / security / performance reviews. |

The split follows [ADR-015](../decisions/0015-roadmap-structure.md): plan and execution
change at different speeds and must not share files.

## Where things live

- *"What are we going to build?"* → [`../roadmap/phases/`](../roadmap/phases/)
- *"What are we doing right now?"* → [`../roadmap/current.md`](../roadmap/current.md)
- *"Which tasks are in flight, and what's their status?"* → [`tasks/`](tasks/)
- *"What did the latest business / code / security review conclude?"* → [`reviews/`](reviews/)

## Rules

1. **No roadmap edits from here.** If a task uncovers a scope change, raise an ADR first;
   then update the phase file; then update `current.md`.
2. **`Done` is maintainer-only.** Contributors mark tasks `In Review`; maintainer promotes.
   Full status vocabulary in ADR-015.
3. **Link, don't duplicate.** Task files reference the owning phase file instead of
   restating scope.

## Related

- [ADR-015 — Roadmap Structure](../decisions/0015-roadmap-structure.md)
- [Tasks README](tasks/README.md)
- [Reviews README](reviews/README.md)
