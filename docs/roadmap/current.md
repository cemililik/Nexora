# Current State

**Last updated:** 2026-04-24 (post-carry-over reconciliation sweep — T-007 split confirmed, T-010 reclassified, T-030 filed, ADR-0031 Accepted)

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

## Phase 1.5 carry-over sweep (2026-04-24)

After the main Phase 1.5 batch closed, a follow-up sweep picked up the
deferred items in priority order:

| Task | Outcome |
|------|---------|
| **T-028** | **Done** — inbox-guarded `ContactExportCompletedNotificationHandler` in Notifications, job no longer sends inline. Exactly-once delivery via EventId dedup. |
| **T-009** | **Done** — `nexora demo:clean` CLI verb, `IDemoDataCleaner` orchestrator (reverse-dep-order module cleanup), `--drop-tenant --yes` full-schema-drop path. `IModule.CleanDemoDataAsync` DIM added. |
| **T-008** | **Done** — `POST /identity/tenants/demo` endpoint (platform-scope, gated on new `platform.tenants.create_demo` permission), `CreateDemoEnvironmentDialog` admin UI with per-module outcome view, `useCreateDemoEnvironment` hook. |
| **Phase 2 priority-1 fixes** | **Done** — ADR-0030 (license hot-reload: polling + SIGHUP; Accepted), T-016 milestone assigned to C, HR SPEC.md header Tier-vs-Phase clarification. |
| **T-007** | **Superseded by [T-029](../analysis/tasks/phase-1.5/T-029.md)** (2026-04-24 reconciliation). The proposed three-way split is now binding: T-029 ships the registry + Contacts seed under Phase 1.5 Milestone C; the per-Phase-2 / per-Phase-3a module seeds are filed at each module's kickoff (no orphan stubs). T-008's hardcoded `general`/`ngo` dropdown stays as the interim measure until T-029 lands and turns it registry-driven. |
| **T-010** | **Reclassified to Phase 2 Milestone A** (2026-04-24 reconciliation). File moved to `docs/analysis/tasks/phase-2/T-010.md`, status reset to **Not started**. Scope at Milestone A is the scaffolding slice (Contacts locator + Audit scan job + arch test); CRM ships the first non-trivial locator inside the same milestone via T-030. 10M-row perf benchmark deferred to Milestone C. |

The sweep also scoped + filed the three orphan uninstall tasks
(T-025 / T-026 / T-027) that ADR-0028 references in their own files
under `docs/analysis/tasks/phase-2/` — they were missing before.

## Phase-2 reconciliation (2026-04-24, post-sweep)

Follow-up disposition of the items the carry-over sweep left provisional:

| Item | Outcome |
|------|---------|
| **T-007 split** | **Confirmed.** T-007 marked `Superseded by T-029` in its status log. [T-029](../analysis/tasks/phase-1.5/T-029.md) filed under Phase 1.5 Milestone C — the registry + Contacts-only seed slice that does not require unbuilt modules. Per-Phase-2 / per-Phase-3a module seeds intentionally not pre-allocated; each owning module files its own seed task at kickoff. |
| **T-010 reclassification** | **Confirmed.** Moved `docs/analysis/tasks/phase-1.5/T-010.md` → `docs/analysis/tasks/phase-2/T-010.md`. Status `Not started`. Milestone A. Scaffolding only (Contacts locator + Audit scan job + arch test); CRM is the first non-trivial locator consumer via T-030. |
| **Phase-1.5.4 portal pilot deferral** | **Filed as [T-030](../analysis/tasks/phase-2/T-030.md)** under Phase 2 Milestone A — backend manifest assembly endpoint, manifest schema artefact, CRM frontend pilot (3 slot kinds), host-loader registry uncomment, license/permission filter integration test, retrospective into `docs/architecture/portal-extensions.md`. |
| **ADR-0028 cascade transaction policy** | **Superseded in part by [ADR-0031](../decisions/0031-cascade-uninstall-per-module-transactions.md)** (Accepted). The single-outer-transaction-with-savepoints mechanism in ADR-0028 §Decision outcome was infeasible under per-module DbContext isolation + ADR-0001's no-DTC rule. ADR-0031 makes per-module transactions + session advisory lock + forward-log compensation normative; ADR-0028's retention window, cleanup job, GDPR escape hatch, dependency guard, extended event schema, and config key all stand. T-026 updated to cite ADR-0031 as the source of its transaction-boundary rule. |
| **Doc hygiene** | T-025/T-026/T-027 phase-file links fixed (`phase-2-enterprise-core.md` → `phase-2-enterprise.md`; relative depth corrected). `docs/analysis/tasks/phase-2/README.md` rewritten to list the 11 filed tasks. `docs/operations/migration-orchestration.md` `Derives from` line gained ADR-0027. `docs/architecture/portal-extensions.md` §8 added to make the manifest-schema canonical-vs-publish path explicit. `docs/analysis/tasks/README.md` ADR-0015 link fixed. `docs/decisions/README.md` index gained ADR-0018..0023, ADR-0031, and the partial-supersede note on ADR-0028. `docs/roadmap/phases/phase-2-enterprise.md` ADR ledger expanded to cite all consumed ADRs (0018..0023, 0026..0031); Milestone A scope expanded to bundle T-010 / T-011 / T-012 / T-025 / T-026 / T-027 / T-030. |



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
- Demo Data Framework (Phase 1.5.7) — **Foundation + admin UI + cleanup Done**
  (T-005 + T-006 closed 2026-04-23; T-008 + T-009 closed in the carry-over sweep
  2026-04-24). T-007 was **Superseded by [T-029](../analysis/tasks/phase-1.5/T-029.md)**;
  T-029 (registry + Contacts seed) ships under this phase, per-module seeds ship
  with each owning Phase 2 / Phase 3a module.

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

_None blocking Phase 1.5 closure or Phase 2 entry._ Five ADRs (0026, 0027, 0028,
0029, 0030) were promoted **Proposed → Accepted** on 2026-04-24 in the same batch
that closed the 14 tasks above. They unblock the Phase-2-adjacent follow-up
tasks listed under "Recently closed" (T-010 via ADR-0026, T-011 via ADR-0027,
T-025..T-027 via ADR-0028, T-014/T-015 via ADR-0030). ADR-0029 is the canonical
record for the `cap.blocked` short-circuit that shipped in commit `576ff65`
and supersedes ADR-0025's resolution ladder.

ADR-0031 was Accepted in the post-sweep reconciliation (2026-04-24) to replace
the cascade transaction mechanism inside ADR-0028 — the original
single-outer-transaction-with-savepoints approach was infeasible under
per-module DbContext isolation + ADR-0001's no-DTC rule. ADR-0031 normatively
adopts per-module transactions + session advisory lock + forward-log
compensation. ADR-0028 retains every other commitment.

ADR-0025 (org-scoped compliance config) remains **Superseded by ADR-0029**
for the resolution-ladder subsection; all other decisions in ADR-0025
remain in force via the superseding doc.

ADR-0028 (module uninstall data-retention contract) carries a **partial
supersede** by ADR-0031 covering only its cascade transaction policy;
the rest of the contract stands.

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
