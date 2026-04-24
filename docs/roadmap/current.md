# Current State

**Last updated:** 2026-04-23

---

## Active task

_None. All 6 tasks in the review-independent batch moved to In Review —
see Prior active tasks._

## Prior active tasks (awaiting maintainer review)

**T-024 — `check-schema-drift.py` CI integration via an opt-in xUnit wrapper**
(Phase 1.5, tooling). Status: **In Review**. `SchemaDriftToolTests` (tagged
`Category=Tooling`) wraps the existing Python drift detector using
`[SkippableFact]`. Default PR CI: Skipped with a one-liner pointing at
`NEXORA_SCHEMA_DRIFT_ENABLED=1`. Opt-in: shells out to
`python3 tools/check-schema-drift.py`, fails with the full stdout + stderr
when exit code is non-zero so the CI log names the drifted column. Precondition
misses (python3 not on PATH, script not found) surface as Skipped — drift is
the only failure mode. Verified in both paths: default Skipped (confirmed in
run output — 1 Skipped across Infrastructure.Tests); opt-in against the live
dev DB passes (confirms T-021 + T-022 cleanup held — zero drift detected).
`docs/standards/schema-migration.md` §5 gains a new Testing bullet.

**T-023 — `/health/ready` dependency-level diagnostics** (Phase 1.5,
observability). Status: **In Review**. `AddHealthChecks` now registers
`PostgresHealthCheck` (SELECT 1 with 2s timeout) and `DaprSidecarHealthCheck`
(HTTP GET `/v1.0/healthz` with 1s timeout) alongside the existing
`OutboxHealthCheck`. Both surface `latency_ms` + impl-specific data in the
envelope so Grafana can chart readiness without a separate metric. Keycloak +
MinIO stay transitive via Dapr (rationale in task file). 7 unit tests cover
every branch (missing config, unreachable host, timeout, 503, thrown exception,
cancellation, URL construction). No endpoint-shape change — existing
ResponseWriter serializes the new checks automatically.

**T-022 — Architecture guard for `HasFilter("\"IsDeleted\"...")` targeting
non-`ISoftDeletable` entities** (Phase 1.5.6, Milestone C — T-021 preventive
follow-up). Status: **In Review**. Scans every module DbContext at test
time, walks each entity's declared indexes, and fails CI when a Filter
references `"IsDeleted"` on a type that doesn't implement `ISoftDeletable`.
Includes a self-test (`DriftProbeContext`) proving the scanner actually
fires so a silent no-op never ships. **Discovery**: on its first run the
guard caught 4 more drift cases in the Identity module —
`OrganizationUser`, `Permission`, `RolePermission`, `UserRole` — all of
which extend `Entity<T>` with dead `HasFilter` clauses. Fixed in the
same commit. No DDL change (affected tables never had the column).

**T-021 — Fix stray `HasFilter("\"IsDeleted\" = false")` on non-soft-deletable Contacts
configs** (Phase 1.5.6, Milestone C — T-018 follow-up). Status: **In Review**.
Drops the dead filter predicate from `ContactTagConfiguration` and
`ContactCustomFieldConfiguration`; entities extend `Entity<T>`, not
`AuditableEntity<T>`, so the `IsDeleted` column never existed on those tables
and the filter was silently dropped at CREATE INDEX time in dev. T-018's
integration test simplified: plain `IRelationalDatabaseCreator.CreateTablesAsync()`
replaces the statement-by-statement 42703-skipping workaround; two helper methods
(~60 LoC) removed. Full suite green. No DDL change in the live dev DB because
the index was already unfiltered there.

**T-006 — `nexora demo:load` CLI command** (Phase 1.5.7, Milestone C).
Status: **In Review**. CLI dispatcher intercepts before `WebApplication.CreateBuilder`
so CLI runs never start the web host (`TryDispatch` returns false for unknown
argv → web host still boots normally). `demo:load --tenant=<guid>
--scenario=<name> [--dry-run]` parses both `--k=v` and `--k v` forms, probes
`pg_namespace` for the tenant schema (clear error + usage-exit code when
missing), runs `IDemoDataSeeder`, and surfaces a per-module summary with an
exit code that distinguishes usage errors (1), partial failures (2), and
success (0). 9 unit tests cover the parser, dispatcher fall-through, dry-run,
schema probe, success, and partial-failure paths. Tenant auto-provisioning +
E2E shell-out test deferred with rationale in the task status log.

**T-005 — `IModule.SeedDemoDataAsync` + orchestrator** (Phase 1.5.7, Milestone C).
Status: **In Review**. Foundation-only: `IModule.SeedDemoDataAsync` (default
no-op via C# default interface method, so all existing modules compile
unchanged), `TenantDemoSeedContext` with a scoped `IServiceProvider`, a
`DemoDataSeeder` orchestrator that topologically sorts by
`IModule.Dependencies` and runs each module in its own DI scope, a
`demo_seed_markers` tenant-schema table for per-(tenant, module,
scenario) idempotency. 8 unit tests (ordering / idempotency / scenario
separation / broken-sibling isolation / cycle + missing-dep detection / tenant
validation) + 2 architecture tests (no cross-module Infrastructure deps + contract
signature lock). T-007 ships scenario content; T-006 ships the CLI.
Full suite green at snapshot `d7aa007`: 1960 passed + 2 skipped (Identity slow-query opt-in + T-024 schema-drift opt-in) across 11 test assemblies.

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
42703 skip with a TODO; T-021 has since cleaned that up). Full suite green at snapshot `d7aa007`: 1960 passed + 2 skipped backend across 11 assemblies, 98 frontend tests.


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
- Demo Data Framework (Phase 1.5.7) — **In Review** (foundation T-005 + CLI T-006
  shipped In Review; T-007 scenarios / T-008 admin UI / T-009 cleanup not started)

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

**ADR-0029 — `cap.blocked` short-circuits lower layers in the compliance-config
resolver** (Proposed 2026-04-24). Supersedes ADR-0025's resolution ladder.
Normative change: when `cap.Allowed = false`, the resolver returns
`cap.Value` (or `default(T)` when null) and skips org + tenant layers
entirely — closes the Article 17 / ADR-0023 EU-tenant gate where a stale
pre-cap org override could leak through reads. Already shipped in `576ff65`;
this ADR is the normative record (moved out of the inline Amendment 1 block
that originally sat in ADR-0025 — ADR-immutability rule treats behavioural
changes as superseding, not amending). Maintainer promotion unblocks nothing
new (code is live) but establishes the canonical decision doc.

**ADR-0028 — Module uninstall data-retention contract** (Proposed 2026-04-23).
Formalizes the current rename-and-retain uninstall pattern: 30-day default
retention (configurable via resolver key `modules.uninstall.retention_days`,
cap-bounded per ADR-0025), mandatory `platform:purge-uninstalled-modules`
cleanup job, dependency-cascade guard, GDPR Article 17 escape hatch that
honours erasure against `_del_{timestamp}` renamed tables, extended
`ModuleUninstalledIntegrationEvent` carrying canonical + renamed table names.
Rejects immediate hard-delete (no recovery, long-lock risk) and per-module
policy sprawl (breaks cross-module reasoning). Three follow-up tasks scoped
in the ADR (T-025 cleanup job, T-026 cascade guard, T-027 GDPR handler).

**ADR-0027 — Production schema-migration strategy** (Proposed 2026-04-23). Closes
the "Production strategy — TBD" marker in `docs/standards/schema-migration.md`
and CLAUDE.md. Chooses EF Core Migrations (module-scoped, per-tenant rollout per
`migration-orchestration.md`) with a CI release-gate that asserts every DDL line
in `DevelopmentSeed.ApplySchemaUpdatesAsync` has a matching EF migration before
the release cut. Rejects Liquibase/Flyway (two-system drift), continuing
`ApplySchemaUpdatesAsync` in prod (no version history, no additive-only CI), and
bespoke versioned SQL files (rebuilds EF without its ecosystem). Unblocks T-011
(MigrationRunner). Related: ADR-0001, ADR-0002, ADR-0003.

**ADR-0026 — Cross-module PII payload scan for GDPR erasure** (Proposed 2026-04-23).
Captures the design decision that T-010 is blocked on: per-module
`IContactReferenceLocator` with declared JSON paths (chosen), vs. full-table regex
scan / write-time structured tagging / no-op. The ADR identifies false-positive
safety as the decisive driver — a regex scan for the erased contact's name would
almost certainly redact unrelated audit rows ("paid Jon's invoice" colliding with
a contact named "Jon"), which would itself be a data-integrity incident. Maintainer
promotion to Accepted unblocks T-010. Related: T-010, ADR-0008, ADR-0023.

ADR-0025 (org-scoped compliance config) was promoted to `Accepted`
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
