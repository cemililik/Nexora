# Phase 1.5 — Bridge

**Tier:** cross-cutting (no new tier; closes Tier-1 gaps + seeds Tier-2 plumbing)
**Status:** In Progress (~80% complete as of 2026-04-22)
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
   (Phase 1.5.4 — **met**).
5. Audit module enhancements — entity change tracking, auth-event auditing, retention +
   partitioning jobs — are live (Phase 1.5.5 — **met**).
6. Contacts Module enhancements (user-contact linking, import wizard, export, GDPR hard
   delete) are merged (Phase 1.5.6 — **in progress**).
7. Demo Data Framework (`SeedDemoData()` in `IModule`, CLI, scenarios, admin UI, cleanup)
   is merged (Phase 1.5.7 — **not started**).

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

### 1.5.4 Portal UI Extension Points — **Done**
`PortalModuleManifest.slots`, `<ModuleSlot>`, `<ModuleTabs>` components, permission
filtering, error-boundary isolation, cross-module contribution pattern (e.g. Finance
contributes a tab into Contacts 360°).

### 1.5.5 Audit Module Enhancements — **Done**
Entity change tracking (before/after via `IAuditStateCapture` + `AuditChangeTrackerInterceptor`),
auth event auditing (`POST /audit/events/auth`), retention/partitioning jobs
(`AuditCleanupJob`, `AuditPartitionMaintenanceJob`).

### 1.5.6 Contact Module Enhancements — **In Progress**
Open items tracked as individual tasks under `../../analysis/tasks/phase-1.5/`:

- User ↔ Contact linking (optional `ContactId?` on User; "Staff" contact type co-designed
  with CRM).
- Contact Import field-mapping wizard (CSV/Excel column-to-field dropdowns).
- Contact Export improvements (custom field selection, date range, vCard).
- GDPR Hard Delete (Article 17 physical removal; cross-module cleanup via
  `ContactGdprDeletedIntegrationEvent`).

### 1.5.7 Demo Data Framework — **Not started**
Open items:

- `SeedDemoData()` method on `IModule`.
- `nexora demo:load` CLI command.
- Pre-built demo scenarios (General business, NGO vertical).
- Admin UI "Create Demo Environment" button.
- Demo data cleanup command.

## Out of scope

- Any Tier-2 business functionality (CRM, Finance, Subscription, Projects) — Phase 2.
- NMP Platform Admin role, license enforcement, tenant CRUD UI redesign — NMP track.
- US/TR tax-receipt templates — Phase 3a Fundraising.
- Tier-4 Extensions and vertical editions.

## Milestones

### Milestone A — Reliability plumbing (Done)
1.5.1 outbox/inbox + cache invalidation, 1.5.5 audit enhancements, 1.5.2 permission
isolation (backend). Gate for Phase 2 financial flows.

### Milestone B — Experience plumbing (Done)
1.5.3 localization (minus tax receipts), 1.5.4 portal UI extension points. Gate for Phase
2 portal surfaces.

### Milestone C — Seed + lifecycle (In Progress)
1.5.6 Contacts enhancements (5 open items), 1.5.7 Demo Data Framework (5 open items).

## Acceptance criteria

- [x] Outbox monitoring dashboards green for 14 consecutive days (1.5.1).
- [x] Platform-scope permissions return empty for tenant-admin callers (1.5.2).
- [x] Tenant locale setting round-trips through report export (1.5.3).
- [x] Cross-module tab contribution works end-to-end (1.5.4).
- [x] Audit partitions auto-created three months ahead (1.5.5).
- [ ] All five Contacts enhancements merged and covered by tests (1.5.6).
- [ ] Demo tenant provisionable via `nexora demo:load` in < 2 minutes (1.5.7).

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
