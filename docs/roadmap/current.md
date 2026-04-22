# Current State

**Last updated:** 2026-04-23

---

## Active task

**T-004 — GDPR Article 17 hard delete** (Phase 1.5.6, Milestone C). Owner: cemililik. Status: In Progress.

- Plan: [T-004.md §Implementation plan](../analysis/tasks/phase-1.5/T-004.md)
- Work packages: WP1 Contacts core (in progress) → WP2/3/4 cross-module handlers → WP5 Admin UI
- Compliance deadline: 2026-09-30 (ADR-0008)



This is the single sync point for "what are we doing right now?". Update it in the same commit
as any phase transition, ADR promotion, or active-milestone change.

---

## Active phase

**Phase 1.5 — Bridge** (in progress, ~80% complete)

Goal: Close the gap between the Phase 1 Core Platform and Phase 2 Enterprise Core by delivering
cross-cutting plumbing (outbox/inbox, cache invalidation, localization, portal extension points,
audit enhancements, demo data framework) plus the last set of Contacts module enhancements.

See [`phases/phase-1.5-bridge.md`](phases/phase-1.5-bridge.md) (to be written by Phase 2 Agent A)
for scope and exit bar. Legacy reference: `docs/roadmap/ROADMAP.md` §1.5 and §1.5.1–1.5.7.

### Active milestones (as of this writing)

- Outbox/Inbox + cache cross-instance invalidation — **Done** (shipped in Phase 1.5.1)
- Tenant permission isolation (backend) — **Done** (shipped in Phase 1.5.2; Platform Admin
  separation deferred to NMP)
- Localization resolution — **Done** (Phase 1.5.3; Phase 3 defers tax-receipt templates)
- Portal UI extension points — **In Progress** (pilot via ADR-017 in Phase 2)
- Audit Module enhancements (Phase 1.5.5) — **Done**
- Contacts enhancements (Phase 1.5.6) — **In Progress** (5 open items; see migration-notes §2.1)
- Demo Data Framework (Phase 1.5.7) — **Not started**

---

## Next phase

**Phase 2 — Enterprise Core** (Tier 2)

Modules: CRM (generic), Subscription & Billing, Finance, Projects. HR Core splits into its own
phase (2.5).

### Entry criteria (must hold before Phase 2 starts)

1. Phase 1.5 exit bar met: outbox/inbox, localization, portal extension points in place.
2. **ADR-015, ADR-016, ADR-017 promoted from `Proposed` → `Accepted`** by the maintainer.
3. `docs/` restructure (this initiative) complete through Phase 2 (five parallel agent outputs
   in `In Review`, maintainer verified).
4. Demo Data Framework scaffold (1.5.7) available so Phase 2 modules can declare demo seeds.

### Pilot commitment

Phase 2 Milestone A pilots the Portal Extension manifest (ADR-017) with one Tier-2 module
(CRM or Subscription, TBD by the maintainer). Lessons feed back into `docs/architecture/portal-extensions.md`.

---

## Pending decisions

None. The three Phase-1 foundation ADRs are **Accepted** (maintainer promoted 2026-04-22):

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-0015](../decisions/0015-roadmap-structure.md) | Roadmap Structure and Phase Model | Accepted |
| [ADR-0016](../decisions/0016-module-tier-classification.md) | Module Tier Classification | Accepted |
| [ADR-0017](../decisions/0017-portal-extension-architecture.md) | Portal Extension Architecture | Accepted |

Phase 2 of the docs-restructure (five parallel agents) may now begin.

---

## Initiative in flight

**Docs restructure (Umbrix-style layout).** Phase 1 (solo, this agent) has produced:

- This `current.md`, `roadmap/README.md`, `phases/README.md` (drafts).
- Three foundation ADRs (above) in `Proposed` status.
- `docs/_archive/migration-notes.md` with the full inventory, tier mapping, and 20-item
  execution plan for Phase 2 agents.

**No existing files have been moved or deleted.** Phase 2 (five parallel agents) executes the
migration after maintainer ADR promotion.

---

## Pointers

- [Roadmap overview](README.md)
- [Phase index](phases/README.md) (stub until Agent A fills it in Phase 2)
- [Decisions (ADRs)](../decisions/)
- [Migration plan](../_archive/migration-notes.md)
