# Phases

One file per phase. Each file is self-contained: **Exit bar, Scope, Out of scope, Milestones,
Acceptance criteria, ADR ledger, Informs**.

> **Note.** This directory is seeded by Phase 1 of the docs-restructure. The actual phase files
> are written by Phase 2 Agent A, sourcing content from
> [`../../_archive/migration-notes.md`](../../_archive/migration-notes.md) §2.

## Index

| Phase | File | Status | Tier focus |
|-------|------|--------|-----------|
| 1 — Foundation | `phase-1-foundation.md` *(retrospective)* | Done | Tier 1 |
| 1.5 — Bridge | `phase-1.5-bridge.md` | In Progress | Tier 1 cross-cutting |
| 2 — Enterprise Core | `phase-2-enterprise.md` | Next | Tier 2 |
| 2.5 — HR | `phase-2.5-hr.md` | Planned | Tier 2 |
| 3a — NGO Edition | `phase-3a-ngo.md` | Planned | Tier 3a |
| 3b — Education Edition | `phase-3b-education.md` | Planned | Tier 3b |
| 4 — Extensions | `phase-4-extensions.md` | Planned | Tier 4 |

### Parallel tracks

| Track | File | Status | Scope |
|-------|------|--------|-------|
| NMP — Nexora Management Portal | [`phase-NMP-track.md`](phase-NMP-track.md) | Planned | Platform-operator product (tenant lifecycle, licensing, billing, marketplace) — runs concurrently with Phases 2 → 4. |

Tracks are not numbered phases: they run in parallel, have their own exit bar, and may span
multiple phase windows. Adding a new track requires either an ADR or a note in
[`../current.md`](../current.md).

## Conventions

- Each phase file is ~200–400 lines, in English.
- Milestones are labelled `A`, `B`, `C` within a phase.
- ADR ledger lists: ADRs this phase **introduces**, **consumes**, or **supersedes**.
- "Informs" is a bullet list of downstream phases/specs that depend on this phase's output.

## Related

- [Roadmap root](../README.md)
- [Current state](../current.md)
- [Decisions](../../decisions/)
