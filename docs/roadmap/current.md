# Current State

**Last updated:** 2026-04-24

---

## Active task

_None._

## Recently closed (Phase 1.5 batch — merged to `main` 2026-04-24)

14 tasks promoted **In Review → Done** after verification review (commit
`c2c5aa0`): AC audited against shipped code, DoD complete, full test
suite green, deferred follow-ups logged as separate task IDs. Task
files remain in `docs/analysis/tasks/phase-1.5/` with their final
status log entries as the historical record.

| Task | Title | Milestone |
|------|-------|-----------|
| T-001 | User ↔ Contact linking | C |
| T-002 | Contact import field-mapping wizard | C |
| T-003 | Contact export improvements (CSV/XLSX/vCard + MinIO) | C |
| T-004 | GDPR Article 17 hard delete | C |
| T-005 | `IModule.SeedDemoDataAsync` orchestrator foundation | C |
| T-006 | `nexora demo:load` CLI command | C |
| T-017 | Notifications `BodyRendered` nullable migration | C |
| T-018 | `NexoraJob` tenant context + org propagation verification | C |
| T-019 | Org-scoped compliance config with platform caps (ADR-0025/0029) | C |
| T-020 | Permission seed consolidation via `IPermissionRegistry` | C |
| T-021 | Fix stray `HasFilter("IsDeleted"=false)` on non-soft-deletable Contacts | C |
| T-022 | Architecture guard for `HasFilter("IsDeleted"…)` drift | C |
| T-023 | `/health/ready` dependency-level diagnostics | — |
| T-024 | `check-schema-drift.py` CI integration (opt-in xUnit wrapper) | — |

**Deferred follow-ups filed as separate tasks** (not blocking Phase 1.5
closure): T-007 (demo-data scenarios), T-008 (demo-data admin UI),
T-009 (demo-data cleanup), T-010 (cross-module PII payload scan —
unblocked by ADR-0026), T-011 (MigrationRunner — unblocked by
ADR-0027), T-025 (uninstall cleanup job), T-026 (uninstall cascade
guard), T-027 (uninstall GDPR handler), T-028 (export/import
notification inbox handler dedup).



This is the single sync point for "what are we doing right now?". Update it in the same commit
as any phase transition, ADR promotion, or active-milestone change.

---

## Active phase

**Phase 1.5 — Bridge** (scaffolding complete as of 2026-04-24; Phase 2 entry-ready)

Goal: Close the gap between the Phase 1 Core Platform and Phase 2 Enterprise Core by delivering
cross-cutting plumbing (outbox/inbox, cache invalidation, localization, portal extension points,
audit enhancements, demo data framework) plus the last set of Contacts module enhancements.

See [`phases/phase-1.5-bridge.md`](phases/phase-1.5-bridge.md) for the full scope, exit bar,
and milestone breakdown. Legacy reference: `docs/roadmap/ROADMAP.md` §1.5 and §1.5.1–1.5.7.

### Milestone status (as of 2026-04-24)

- Outbox/Inbox + cache cross-instance invalidation — **Done** (Phase 1.5.1)
- Tenant permission isolation (backend) — **Done** (Phase 1.5.2; Platform Admin separation
  deferred to NMP)
- Localization resolution — **Done** (Phase 1.5.3; Phase 3 defers tax-receipt templates)
- Portal UI extension points (Phase 1.5.4) — **Scaffolding Done** (slots, tabs, permission
  filter, error boundary, unit tests all shipped; no module contributes to a slot yet —
  end-to-end pilot is Phase 2 Milestone A per [ADR-0017](../decisions/0017-portal-extension-architecture.md)
  "First pilot" section)
- Audit Module enhancements (Phase 1.5.5) — **Done**
- Contacts enhancements (Phase 1.5.6) — **Done** (T-001..T-004 + T-017..T-022 closed 2026-04-24;
  cross-module PII payload scan follow-up tracked as T-010, unblocked by ADR-0026)
- Demo Data Framework (Phase 1.5.7) — **Foundation Done** (T-005 orchestrator + T-006 CLI
  closed; T-007 scenarios / T-008 admin UI / T-009 cleanup carry over to Phase 2 — they do
  not block Phase 2 entry because the foundation lets Phase 2 modules declare demo content
  from day one)

---

## Next phase

**Phase 2 — Enterprise Core** (Tier 2)

Modules: CRM (generic), Subscription & Billing, Finance, Projects. HR Core splits into its own
phase (2.5).

### Entry criteria (must hold before Phase 2 starts)

1. Phase 1.5 exit bar met: outbox/inbox ✅, localization ✅, portal extension points
   **scaffold** ✅ (pilot is Milestone A's own work, not an entry gate), Contacts
   enhancements ✅, Demo Data Framework foundation ✅ — **all met as of 2026-04-24**.
2. **ADR-0015, ADR-0016, ADR-0017 Accepted** ✅ (maintainer promoted 2026-04-22).
3. `docs/` restructure (this initiative) complete through Phase 2 (five parallel agent outputs
   in `In Review`, maintainer verified) ✅ — **met as of 2026-04-24**. All five agent
   outputs are in place and have been consumed by the Phase 1.5 work that shipped on top
   of them (7 phase files including phase-2-enterprise, 14 standards, 29 ADRs under the
   new numbering, 4 tier folders with module specs, architecture docs including
   portal-extensions.md, analysis/tasks infrastructure). Verification happened in-place
   as the Phase 1.5 batch (T-001..T-024) depended on and refined each agent's output.
4. Demo Data Framework scaffold (1.5.7) available so Phase 2 modules can declare demo seeds ✅.

### Pilot commitment

Phase 2 Milestone A pilots the Portal Extension manifest (ADR-0017) with **CRM**
(selected 2026-04-24). CRM exercises more of the manifest surface than
Subscription — multiple slot contributions (Contacts 360° tab, dashboard
widgets), no external-payment-gateway entanglement, and sits on the critical
path for Phase 3a/3b downstream dependencies. See
[`phases/phase-2-enterprise.md`](phases/phase-2-enterprise.md) §Milestone A for
the full rationale. Lessons feed back into `docs/architecture/portal-extensions.md`.

---

## Pending decisions

_None blocking Phase 1.5 closure._ Four ADRs (0026, 0027, 0028, 0029)
were promoted **Proposed → Accepted** on 2026-04-24 in the same batch
that closed the 14 tasks above. They unblock the Phase-2-adjacent
follow-up tasks listed under "Recently closed" (T-010 via ADR-0026,
T-011 via ADR-0027, T-025..T-027 via ADR-0028). ADR-0029 is the
canonical record for the `cap.blocked` short-circuit that shipped in
commit `576ff65` and supersedes ADR-0025's resolution ladder.

ADR-0025 (org-scoped compliance config) remains **Superseded by ADR-0029**
for the resolution-ladder subsection; all other decisions in ADR-0025
remain in force via the superseding doc.

The three Phase-1 foundation ADRs are **Accepted** (maintainer promoted 2026-04-22):

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
- Three foundation ADRs (above), now `Accepted` (maintainer promoted 2026-04-22).
- `docs/_archive/migration-notes.md` with the full inventory, tier mapping, and 20-item
  execution plan for Phase 2 agents.

**No existing files have been moved or deleted.** Phase 2 (five parallel agents) executes the
migration (maintainer ADR promotion complete 2026-04-22).

---

## Pointers

- [Roadmap overview](README.md)
- [Phase index](phases/README.md) (stub until Agent A fills it in Phase 2)
- [Decisions (ADRs)](../decisions/)
- [Migration plan](../_archive/migration-notes.md)
