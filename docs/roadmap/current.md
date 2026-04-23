# Current State

**Last updated:** 2026-04-23

---

## Active task

_None. T-017 and T-018 both moved to In Review — see Prior active tasks._

## Prior active tasks (awaiting maintainer review)

**T-017 — Notifications BodyRendered nullable migration** (Phase 1.5.6, Milestone C —
T-004 follow-up). Status: **In Review**. Drops NOT NULL on
`notifications_notifications.BodyRendered` via `ApplySchemaUpdatesAsync`; EF config
`IsRequired(false)`; `Notification.ScrubRenderedBody()` now writes null (Subject keeps
its placeholder because its column stays NOT NULL); handler `SetProperty(n => n.BodyRendered, (string?)null)`;
`NotificationDetailDto.BodyRendered` is now `string?`; SPEC §PII retention references
T-017 and explains the null-body / placeholder-subject split. Manually verified: the
ALTER ran clean against the dev tenant schema, `\d` confirms nullable. Tests updated
— 247 Notifications tests + full suite green.

**T-018 — Verify NexoraJob tenant context + org propagation for tenant-scoped seed**
(Phase 1.5.6, Milestone C — T-004 follow-up). Status: **In Review**. Adds two
integration tests against a real Postgres 17 container proving `GdprHardDeleteJob`
routes to the correct tenant schema and that `NexoraJob.RunAsync` logs the tenant
id before any DB work. Three architecture guards in `NexoraJobBoundaryTests`
(source-level SetTenant-before-ExecuteAsync check, `RunAsync` non-virtual, no
subclass-shadowing). XML-doc contract on `ITenantConfiguration` makes the
tenant-scoped (no-org) semantics load-bearing. New audit-coverage.md §2 subsection
documents that platform-init writes are deliberately not audited — operator
mutations flow through already-audited paths. Discovered a pre-existing
`HasFilter` drift on `ContactTag` / `ContactCustomField` unique indexes (logged
in the task status log for maintainer triage; integration test has a targeted
42703 skip with a TODO). Full suite green (~1945 backend + 98 frontend).

## Pending follow-ups (for maintainer triage)

- Pre-existing `HasFilter("\"IsDeleted\" = false")` drift on
  `ContactTagConfiguration` and `ContactCustomFieldConfiguration` — those
  entities extend `Entity<T>`, not `AuditableEntity<T>`, so the filter references
  a column that does not exist. Dev works only because its schema has been
  patched by hand; a fresh Testcontainers Postgres fails with 42703. Surfaces
  as a workaround in T-018's integration test; should be fixed in its own task.


**T-020 — Permission seed consolidation via IPermissionRegistry** (Phase 1.5.6, Milestone C).
Status: **In Review**. Code-to-standard alignment with ADR-004 and `permissions.md` §3.
`IPermissionRegistry` + `InMemoryPermissionRegistry` added. All 6 modules now register their
own permissions in `OnStartupAsync`. `IdentityModuleMigration.SeedAsync` reads the registry
(hardcoded cross-module list removed). `DevelopmentSeed.CreateDefaultPermissions()` deleted.
Role seed enforces scope rule: tenant "Platform Admin" gets **only** `PermissionScope.Tenant`
permissions; 5 pre-existing Platform-scope assignments auto-stripped on first post-deploy
startup. 10 new registry unit tests + 3 architecture boundary tests (no hardcoded perm
strings outside module `OnStartupAsync`). Full suite green (951 passing). No new ADR — this
task closes the tech debt that ADR-004 (Accepted) had already foreseen.

**T-019 — Org-scoped compliance config with platform caps** (Phase 1.5.6, Milestone C).
Status: **In Review**. ADR-0025 implementation: `IConfigurationResolver` (3-tier
precedence cap → tenant → org) + `DatabaseConfigurationResolver` with L1/L2 cache +
`NullComplianceCapProvider`. Two new tenant-schema tables (`platform_org_config`,
`platform_compliance_policy_audit`). Two new permissions (`contacts.gdpr.settings_manage`
tenant + `platform.compliance.policy_manage` platform). `RequestGdprDeleteHandler` swapped
to resolver — behaviour unchanged. Admin compliance settings page at
`/identity/settings/compliance` + 30 new en/tr lockey keys. 11 resolver unit tests +
1 architecture test + 13 updated GDPR tests; full suites green. Runtime smoke: 34
schema statements applied, no errors.

**T-003 — Contact export improvements** (Phase 1.5.6, Milestone C). Status: **In Review**.
ExportJob entity + Hangfire `contacts:bulk-export` + CSV/XLSX/vCard 3.0 generators + MinIO
presigned downloads + `ILocaleContext` formatting + `ContactExportCompletedIntegrationEvent`
via outbox + status polling endpoint (`GET /export/{jobId}`). Frontend: `ExportFieldPicker`
checkbox list, extended `ExportPage` (format/fields/date range/filters + status panel with
download), `useExportStatus` polling hook, en + tr locale parity, SPEC §UC-CON-004b with
Mermaid sequence. 33 backend tests + 6 frontend tests green; lint clean.

**T-002 — Contact import field-mapping wizard** (Phase 1.5.6, Milestone C). Status: **In Review**.
3-step wizard (upload/preview → mapping → validate → confirm), dynamic parser, `IContactDuplicateMatcher`
extraction, `[Queue("bulk")]` + `contacts:bulk-import` descriptor, `ColumnMappingJson` persisted on `ImportJob`,
42 new locale keys en+tr parity, SPEC §UC-CON-004 rewritten with Mermaid sequence. 138 backend + 7 frontend
tests green.

**T-001 — User ↔ Contact linking** (Phase 1.5.6, Milestone C). Status: **In Review**.
Identity `User.ContactId?` + link/unlink commands + `UserContactLinkedIntegrationEvent`
via outbox; system-account rejection enforced at domain + handler + UI; GDPR erasure
inbox consumer closes T-004's deferred unlink gap. 19 targeted tests + 280/281 Identity
suite + 72/72 architecture tests + 4/4 frontend dialog tests + lint clean.

**T-004 — GDPR Article 17 hard delete** (Phase 1.5.6, Milestone C). Status: **In Review**.
Shipped on development (7 commits): 21f82c7, 0bcb703, abefb12, d9ab70d, 20f5086, 95ec5d6,
b278805. Amendment PR (b278805) addresses 18/20 maintainer review findings; 3 follow-up
tasks filed (T-010, T-017, T-018). All 30 GDPR tests green; full suite ~1700 pass.
Maintainer review queue.



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

None currently open. ADR-0025 (org-scoped compliance config) was promoted to `Accepted`
on 2026-04-23; T-019 unblocked, implementation shipped, now In Review (see Prior active tasks).

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
