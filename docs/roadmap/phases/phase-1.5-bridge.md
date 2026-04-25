# Phase 1.5 — Bridge

**Tier:** cross-cutting (no new tier; closes Tier-1 gaps + seeds Tier-2 plumbing)
**Status:** Scaffolding complete as of 2026-04-24 (Contacts + Demo-Data foundation shipped; Portal UI extension pilot + Demo-Data scenarios/UI/cleanup carry over to Phase 2)
**Dates:** 2026-Q1 → 2026-Q2

The bridge phase closes cross-cutting gaps between the Tier-1 foundation and the Tier-2
enterprise core. It ships the reliability primitives (outbox/inbox, cache invalidation,
audit enhancements) that Phase 2's financial and CRM flows depend on, plus the last
Contacts enhancements and a demo-data framework so Phase 2 modules can declare seed data.

---

## Exit bar

Phase 1.5 is complete when **all** of the following hold:

1. Outbox/Inbox + cache cross-instance invalidation are in production with monitoring and
   cleanup jobs running on schedule (Phase 1.5.1 — **met**).
2. Backend tenant permission isolation is enforced; platform-scope permissions are hidden
   from tenant admin surfaces (Phase 1.5.2 — **met**; Platform Admin role and license
   limits deferred to NMP).
3. Two-tier locale model + tenant regional defaults are live; en/tr translation coverage
   is at parity across Tier-1 modules (Phase 1.5.3 — **met**; tax-receipt templates
   deferred to Phase 3a Fundraising).
4. Portal UI extension points (slots, tabs, navigation) are shipped and covered by tests
   (Phase 1.5.4 — **scaffold met**; end-to-end pilot deferred to Phase 2 Milestone A per
   [ADR-0017](../../decisions/0017-portal-extension-architecture.md) "First pilot").
5. Audit module enhancements — entity change tracking, auth-event auditing, retention +
   partitioning jobs — are live (Phase 1.5.5 — **met**).
6. Contacts Module enhancements (user-contact linking, import wizard, export, GDPR hard
   delete) are merged (Phase 1.5.6 — **met** as of 2026-04-24; T-001..T-004 closed).
7. Demo Data Framework (`SeedDemoData()` in `IModule`, CLI, scenarios, admin UI, cleanup)
   is merged (Phase 1.5.7 — **foundation met**; T-005 + T-006 closed 2026-04-24;
   T-007 scenarios / T-008 admin UI / T-009 cleanup carry over as Phase 2 follow-ups).

This exit bar is also the entry criterion for Phase 2 (see `../current.md`).

## Scope

### 1.5.1 Outbox / Inbox + Cache Cross-Instance Invalidation — **Done**

Transactional outbox, idempotent inbox (EventId dedup), monitoring, cleanup jobs
(OutboxCleanupJob 7d, InboxCleanupJob 30d), Dapr pub/sub cache invalidation, Email/SMS
Kafka migration, five new integration events, permission cache invalidation.

### 1.5.2 Tenant Permission Isolation (backend only) — **Done (partial)**

`PermissionScope` enum (`Platform` | `Tenant`), `ILicenseVerifier` + `NullLicenseVerifier`,
`platform_license_cache` table, `DeploymentMode` setting, platform-scope permissions hidden
from tenant admin UI. **Deferred items:** Platform Admin role separation (requires Keycloak
realm changes; tracked to NMP), license-based user/org caps (requires NMP license
enforcement).

### 1.5.3 Localization — **Done (partial)**

2-tier locale model (`User.PreferredLanguage` + `TenantSettings` JSONB), `ILocaleContext`
scoped service, tenant locale settings UI + API, locale-aware formatters, coverage audit.
**Deferred item:** US tax-receipt and TR bağış makbuzu templates — requires `Receipt` /
`DonationReceipt` domain, carried to Phase 3a Fundraising.

### 1.5.4 Portal UI Extension Points — **Scaffolding Done; pilot in Phase 2 Milestone A**

`PortalModuleManifest.slots`, `<ModuleSlot>`, `<ModuleTabs>` components, permission
filtering, error-boundary isolation, and the cross-module contribution pattern are
all shipped and unit-tested in `src/Clients/nexora-portal/`. **No module currently
contributes to a slot** — the `_registry.ts` module list is still commented out
pending the first real pilot. Per [ADR-0017](../../decisions/0017-portal-extension-architecture.md)
"First pilot" section, Phase 2 Milestone A ships either CRM or Subscription fully
through this mechanism, which is the end-to-end proof. Until then, 1.5.4 is
"scaffold available, pilot deferred" — by design, not by accident.

### 1.5.5 Audit Module Enhancements — **Done**

Entity change tracking (before/after via `IAuditStateCapture` + `AuditChangeTrackerInterceptor`),
auth event auditing (`POST /audit/events/auth`), retention/partitioning jobs
(`AuditCleanupJob`, `AuditPartitionMaintenanceJob`).

### 1.5.6 Contact Module Enhancements — **Done** (closed 2026-04-24)

All four open items shipped and closed after maintainer review. See
`../../analysis/tasks/phase-1.5/` for the individual task files; each is now
`Status: Done` with its final status-log entry as the archival record.

- T-001 User ↔ Contact linking — Done.
- T-002 Contact Import field-mapping wizard — Done.
- T-003 Contact Export improvements (custom field selection, date range, vCard) — Done.
- T-004 GDPR Hard Delete (Article 17 physical removal; cross-module cleanup via
  `ContactGdprDeletedIntegrationEvent`) — Done.

Follow-ups surfaced during review and filed as separate tasks: T-010
(cross-module PII payload scan for GDPR, unblocked by [ADR-0026](../../decisions/0026-cross-module-pii-payload-scan-for-gdpr-erasure.md)),
T-017/T-018/T-021/T-022 (all now Done), T-028 (export/import notification
inbox dedup, carry-over).

### 1.5.7 Demo Data Framework — **Foundation Done; scenarios/UI/cleanup carry over**

Foundation shipped 2026-04-24 (T-005 + T-006 closed). The remaining three
items are tracked as Phase 1.5 → Phase 2 carry-over tasks:

- T-005 `IModule.SeedDemoDataAsync` + `DemoDataSeeder` orchestrator + `demo_seed_markers`
  idempotency table — **Done**.
- T-006 `nexora demo:load` CLI command (dispatcher, parser, dry-run, exit codes,
  9 unit tests) — **Done**.
- T-007 Pre-built demo scenarios (General business, NGO vertical) — **Not started**.
- T-008 Admin UI "Create Demo Environment" button — **Not started**.
- T-009 Demo data cleanup command — **Not started**.

## Out of scope

- Any Tier-2 business functionality (CRM, Finance, Subscription, Projects) — Phase 2.
- NMP Platform Admin role, license enforcement, tenant CRUD UI redesign — NMP track.
- US/TR tax-receipt templates — Phase 3a Fundraising.
- Tier-4 Extensions and vertical editions.

## Milestones

### Milestone A — Reliability plumbing (Done)

1.5.1 outbox/inbox + cache invalidation, 1.5.5 audit enhancements, 1.5.2 permission
isolation (backend). Gate for Phase 2 financial flows.

### Milestone B — Experience plumbing (Scaffolding Done; pilot in Phase 2)

1.5.3 localization (minus tax receipts), 1.5.4 portal UI extension points (scaffold
only — no module contributes to a slot yet; end-to-end pilot is Phase 2 Milestone A
per ADR-0017). Gate for Phase 2 portal surfaces is therefore "scaffold ready to
receive the first pilot module", not "end-to-end contribution proven".

### Milestone C — Seed + lifecycle (Mostly Done)

1.5.6 Contacts enhancements closed 2026-04-24 (4 tasks Done: T-001..T-004 + 6
follow-up review tasks T-017..T-022 closed the same day). 1.5.7 Demo Data
Framework: foundation Done (T-005 + T-006); CLI cleanup (T-009) and admin UI
(T-008) Done in the 2026-04-24 carry-over sweep. Scenarios (T-007) remain
Blocked because the catalogue refers to modules that ship in Phase 2 / 3a
(CRM, Finance, Projects, Fundraising, Sponsorship) — see T-007's status
log for the proposed three-way split. None of the carry-overs block Phase 2
entry: the foundation + CLI + admin surface let module authors declare demo
content from Phase 2 Milestone A onward, and the T-008 dialog ships with a
hardcoded `general`/`ngo` dropdown as the interim measure until T-007a
(scenario registry) lands.

## Acceptance criteria

- [x] Outbox monitoring dashboards green for 14 consecutive days (1.5.1).
- [x] Platform-scope permissions return empty for tenant-admin callers (1.5.2).
- [x] Tenant locale setting round-trips through report export (1.5.3).
- [~] Cross-module tab contribution works end-to-end (1.5.4) — **scaffold shipped
      and unit-tested; end-to-end proof (one module contributing a tab into another
      module's page) deferred to Phase 2 Milestone A per ADR-0017 "First pilot"**.
- [x] Audit partitions auto-created three months ahead (1.5.5).
- [x] All four Contacts enhancements merged and covered by tests (1.5.6) — T-001..T-004
      Done 2026-04-24; the "fifth" (cross-module PII audit scan) was explicitly scoped
      out as the separate T-010 carry-over per ADR-0026.
- [~] Demo tenant provisionable via `nexora demo:load` in < 2 minutes (1.5.7) —
      foundation shipped; the "< 2 minutes" wall-clock check needs T-007 scenarios to
      produce meaningful content. Foundation AC (T-005 + T-006) met; scenario-level
      AC carries over with T-007.

## ADR ledger

**Introduces:** none (Phase 1.5 closes existing work; scope-gated by prior ADRs).

**Consumes:**

- ADR-005 — Transactional Outbox (1.5.1).
- ADR-008 — GDPR deletion strategy (1.5.6 hard delete).
- ADR-010 — Notification delivery via Kafka (1.5.1 Email/SMS migration).
- ADR-011 — Outbox service atomicity (1.5.1).
- ADR-013 — Cache cross-instance invalidation (1.5.1).
- ADR-014 — Distributed consistency patterns (1.5.1 inbox dedup).

**Supersedes:** none.

## Informs

- `phase-2-enterprise.md` — Tier-2 handlers rely on outbox for cross-module events and on
  the demo-data framework for seed scenarios.
- `phase-3a-ngo.md` — Fundraising inherits the deferred tax-receipt template work.
- NMP track — Platform Admin role and license-limit items defer into NMP scope.

## Implementation-plan references

- `../OUTBOX_INBOX_PATTERN_PLAN.md` — detailed design for 1.5.1 (to be archived alongside
  the legacy ROADMAP by Agent E).
- `../SOFT_DELETE_MIGRATION_PLAN.md` — already executed; slated for archive.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §1.5.1–§1.5.7.
- Migration plan: `../../_archive/migration-notes.md` §2.1.
- Status dashboard: [`../current.md`](../current.md).
