# Business Reviews

Product- and strategy-level reviews. Unlike code or security reviews, business reviews
coordinate **multiple perspectives** on where the product is heading and whether the
roadmap still matches reality.

See [`master-plan.md`](master-plan.md) for the five-agent coordination model
(Chronicler, Plan-diff, Learning, Adjuster, Pathfinder) and the merge step.

## Cadence

- At every phase transition (maintainer-triggered).
- Ad hoc when strategy shifts (e.g. a vertical edition's priority changes).

## File naming

`YYYY-MM-DD-<short-slug>.md` — e.g. `2026-06-01-phase-2-kickoff.md`.

## Outputs

A business review may produce:

- One or more new ADRs (scope change, tier re-classification).
- Updates to one or more phase files.
- A `current.md` update (if the active phase changes).
- New or re-prioritized tasks under `../../tasks/`.

## Related

- [Master plan](master-plan.md)
- [Reviews root](../README.md)
