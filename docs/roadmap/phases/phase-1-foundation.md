# Phase 1 — Foundation (Platform Core)

**Tier:** 1 — Platform Core
**Status:** Done (retrospective)
**Dates:** Project start → 2026-Q1

This file is retrospective. Phase 1 is closed; it documents what shipped so Phase 2 consumers
can reason about dependencies without re-reading the legacy ROADMAP.

---

## Exit bar

All eight Tier-1 Platform Core modules live in production, with:

1. Modular Monolith boundaries enforced by architecture tests (ADR-001).
2. Schema-per-tenant multi-tenancy via PostgreSQL (ADR-002).
3. Permission-based RBAC with centralized seeding (ADR-004, ADR-006).
4. GDPR deletion strategy defined (ADR-008) and implemented for Contacts (soft + anonymize;
   Article 17 hard-delete is a Phase 1.5 item, see `phase-1.5-bridge.md`).
5. Localization via `lockey_` keys end-to-end; zero hardcoded user-facing strings in the
   Tier-1 codebase.
6. Tab-based detail layout applied on every Tier-1 module page (ADR-007).

## Scope — what shipped

The eight Tier-1 modules (members of Tier 1 per ADR-016):

| # | Module | Role |
|---|--------|------|
| 1 | **Identity** | Tenants, organizations, users, roles, permissions, Keycloak integration |
| 2 | **Contacts** | Unified contact registry, tags, custom fields, consent, relationships, GDPR |
| 3 | **Notifications** | Email + SMS delivery via Kafka; templates; recipient preferences |
| 4 | **Documents** | Storage (MinIO), signatures, folders, presigned URLs |
| 5 | **Audit** | Entity change tracking, auth event logging, retention & partitioning |
| 6 | **Reporting** | SQL-based reports, Excel/PDF/CSV/JSON export, locale-aware formatting |
| 7 | **Portal Framework** | Next.js portal shell, module slot/tab extension points |
| 8 | **Admin Dashboard** | React 19 admin shell, sidebar/topbar, shared UI primitives |

Cross-cutting foundations also shipped in Phase 1:

- Transactional Outbox / Inbox pattern (moved fully stable in Phase 1.5.1).
- Dapr-based cache service with two-tier (L1 in-memory, L2 Redis) strategy.
- Hangfire jobs with `NexoraJob<TParams>` base class (tenant-aware, traced).
- OpenTelemetry-based observability; structured logging via Serilog.
- ApiEnvelope<T> response contract.

## Out of scope

- Any Tier-2 module (CRM, Subscription, Finance, Projects, HR Core) — Phase 2 / 2.5.
- NMP (Nexora Management Portal) billing and licensing — parallel track with Phase 2.
- Vertical Edition modules (NGO, Education) — Phase 3a / 3b.
- Marketplace Extensions (POS, Fleet, Inventory, Surveys, CMS) — Phase 4.
- Portal Extension manifest (ADR-017 pilot) — Phase 2 Milestone A.

## Milestones (retrospective)

### Milestone A — Platform skeleton
Modular monolith scaffold, schema-per-tenant, Keycloak realm provisioning, CI/CD baseline.

### Milestone B — Tier-1 module vertical slices
All eight Tier-1 modules shipped with CRUD, permissions, localization, tab-based UI, tests.

### Milestone C — Cross-cutting plumbing
Outbox pattern, audit module enhancements (Phase 1.5.5 folded in), cache invalidation
(Phase 1.5.1 folded in), observability, GDPR soft-delete path.

## Acceptance criteria (met)

- [x] Architecture tests enforce module boundaries (one test per boundary).
- [x] All eight Tier-1 modules pass contract tests (ApiEnvelope, permissions, lockey keys).
- [x] GDPR soft-delete + anonymize path covered for Contacts.
- [x] Locale files (en + tr) at full parity across all Tier-1 modules.
- [x] Every Tier-1 detail page uses the custom underline tab layout (UX_UI_STANDARDS §3).
- [x] All fourteen foundational ADRs (ADR-001..014, incl. amendments) are Accepted.

## ADR ledger

**Introduced (all now Accepted):**

- ADR-001 — Modular Monolith
- ADR-002 — Schema-per-tenant multi-tenancy
- ADR-003 — Deployment strategy
- ADR-004 — Centralized permission seeding
- ADR-005 — Transactional Outbox pattern (+ amendment 1)
- ADR-006 — Permission-based authorization
- ADR-007 — Tab-based layout standard
- ADR-008 — GDPR deletion strategy
- ADR-009 — Audit module repository pattern
- ADR-010 — Notification delivery via Kafka
- ADR-011 — Outbox service atomicity (+ addendum 1)
- ADR-012 — Tenant management decisions
- ADR-013 — Cache cross-instance invalidation
- ADR-014 — Distributed consistency patterns

**Consumed:** none (Phase 1 is the foundation layer).

**Supersedes:** none.

## Informs

- `phase-1.5-bridge.md` — consumes every Phase 1 ADR; closes outstanding cross-cutting items
  (demo data, Contacts enhancements, GDPR hard-delete).
- `phase-2-enterprise.md` — every Tier-2 module depends on Tier-1 modules per ADR-016.
- `phase-2.5-hr.md`, `phase-3a-ngo.md`, `phase-3b-education.md`, `phase-4-extensions.md` —
  all downstream tiers consume the Tier-1 surface area frozen here.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §1–§1.5 (pre-split).
- Migration plan: `../../_archive/migration-notes.md`.
- Tier definition: [ADR-016](../../decisions/ADR-016-module-tier-classification.md).
