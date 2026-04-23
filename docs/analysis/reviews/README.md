# Reviews

Review artifacts produced during Nexora execution. Each subdirectory owns one review
kind. A review is **not** a specification — it is a point-in-time reading that informs
decisions elsewhere (ADRs, phase files, tasks).

## Subdirectories

| Folder | Purpose |
|--------|---------|
| [`business-reviews/`](business-reviews/) | Product / strategy reviews; includes the master plan that coordinates five agent roles (Chronicler, Plan-diff, Learning, Adjuster, Pathfinder). |
| [`code-reviews/`](code-reviews/) | PR-level and comparative code-review findings. |
| [`security-reviews/`](security-reviews/) | Threat model snapshots, pen-test reports, security audits. |
| [`performance-reviews/`](performance-reviews/) | Load tests, profiling runs, SLO reviews. |

## Conventions

- One file per review instance. Name: `YYYY-MM-DD-<slug>.md`.
- Every review links to: the phase file, the tasks it touched, and any ADRs it triggered.
- Reviews are **immutable** once filed (like ADRs). Follow up in a new review.

## Related

- [Analysis root](../README.md)
- [ADR-015 — Roadmap Structure](../../decisions/ADR-015-roadmap-structure.md)
