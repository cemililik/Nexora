# Migration Notes — Docs Restructure (Phase 1 Inventory)

## Status
Draft — prepared by Phase 1 solo agent. Actual execution happens in Phase 2.

## Date
2026-04-22

## Purpose

Nexora `docs/` is being reorganized to the Umbrix-style layout (roadmap + analysis + decisions +
standards + modules/tiers + architecture + archive). This document is the authoritative map for
Phase 2 agents: it records the current inventory, the target location for every existing artifact,
and the list of open items extracted from the legacy `docs/roadmap/ROADMAP.md` that must be
carried forward into the new `docs/roadmap/phases/*` files.

**No files are moved, renamed, or deleted in Phase 1.** This document plans; Phase 2 agents execute.

---

## 1. Current Inventory

### 1.1 Docs tree (markdown files)

| Path | One-line summary |
|------|------------------|
| `docs/README.md` | Top-level docs index |
| `docs/PROJECT_VISION.md` | Product vision narrative |
| `docs/architecture/OVERVIEW.md` | High-level architecture overview |
| `docs/architecture/MODULE_SYSTEM.md` | Module loader, `IModule` contract, plugin model |
| `docs/architecture/MANAGEMENT_PORTAL.md` | NMP (Nexora Management Portal) architecture |
| `docs/architecture/COMMUNICATION_FLOW.md` | Request path, Dapr, APISIX, observability flow |
| `docs/auth/IDENTITY.md` | Identity/auth flow notes |
| `docs/code-reviews/*.md` | 33 review artifacts (PR reviews, comparative analyses, audits) |
| `docs/decisions/ADR-001..ADR-014*.md` | 14 ADRs + 2 amendments (modular monolith through distributed consistency) |
| `docs/diagrams/module-dependencies.md` | Module dependency Mermaid graph |
| `docs/guides/API_INTEGRATION_GUIDE.md` | API integration how-to |
| `docs/modules/{module}/SPEC.md` | 20 module specs (see §1.3) |
| `docs/operations/HELM_INSTALLATION.md` | Helm install runbook |
| `docs/operations/TENANT_OPERATIONS.md` | Tenant lifecycle runbook |
| `docs/roadmap/ROADMAP.md` | Legacy monolithic roadmap (1211 lines) |
| `docs/roadmap/OUTBOX_INBOX_PATTERN_PLAN.md` | Outbox/inbox implementation plan (569 lines) |
| `docs/roadmap/SOFT_DELETE_MIGRATION_PLAN.md` | Soft-delete migration plan (371 lines) |
| `docs/standards/*.md` | 11 standard docs (see §1.5) |
| `docs/testcases/00..09-*.md` | 10 test-case matrices per module |

### 1.2 `.claude/skills/`

| Skill | Status |
|-------|--------|
| `add-cqrs-handler` | Keep — codegen utility |
| `add-hangfire-job` | Keep — codegen utility |
| `add-lockey-key` | Keep — localization helper |
| `create-admin-page` | Keep — scaffolding |
| `create-adr` | Keep — will be superseded/merged with new `write-adr` |
| `create-backend-module` | Keep — scaffolding |
| `integration-event` | Keep — codegen utility |
| `pre-commit-verify` | Keep — verification skill |
| `write-module-spec` | Keep — doc scaffolding |

Missing skills to add in Phase 2 (Agent A): `start-task`, `write-adr`, `perform-code-review`,
`perform-security-review`, `conduct-business-review`, `write-architecture-doc`, `supersede-adr`.

### 1.3 Module spec inventory (tier mapping)

| Current spec | Target tier folder | Action |
|-------------|-------------------|--------|
| `modules/identity/SPEC.md` | `tier-1-core/identity/SPEC.md` | move + add Tier header |
| `modules/contacts/SPEC.md` | `tier-1-core/contacts/SPEC.md` | move + add Tier header; NGO examples cleaned in Prompt 02 |
| `modules/notifications/SPEC.md` | `tier-1-core/notifications/SPEC.md` | move + header |
| `modules/documents/SPEC.md` | `tier-1-core/documents/SPEC.md` | move + header |
| `modules/audit/SPEC.md` | `tier-1-core/audit/SPEC.md` | move + header |
| `modules/reporting/SPEC.md` | `tier-1-core/reporting/SPEC.md` | move + header |
| *(missing)* portal-framework | `tier-1-core/portal-framework/SPEC.md` | create stub (Agent D) |
| *(missing)* admin-dashboard | `tier-1-core/admin-dashboard/SPEC.md` | create stub (Agent D) |
| `modules/crm/SPEC.md` | `tier-2-enterprise/crm/SPEC.md` | move + header; donor-acquisition language removed in Prompt 02 |
| `modules/accounting/SPEC.md` | `tier-2-enterprise/finance/SPEC.md` | **rename accounting → finance** + move + header; B2B revision in Prompt 02 |
| `modules/projects/SPEC.md` | `tier-2-enterprise/projects/SPEC.md` | move + header (stub) |
| `modules/hr/SPEC.md` | `tier-2-enterprise/hr/SPEC.md` | move + header (stub) |
| `modules/subscription/SPEC.md` | `tier-2-enterprise/subscription/SPEC.md` | move + header; content rewrite in Prompt 02 |
| `modules/donations/SPEC.md` | `tier-3a-ngo/fundraising/SPEC.md` | **merge with sponsorship** into `fundraising`; Prompt 02 writes merged spec |
| `modules/sponsorship/SPEC.md` | `tier-3a-ngo/fundraising/SPEC.md` | same as above (merged target) |
| `modules/events/SPEC.md` | `tier-3a-ngo/events-ngo/SPEC.md` | move + rename to `events-ngo`; generic event chunks may later split to Tier 2 |
| `modules/education/SPEC.md` | `tier-3b-education/education/SPEC.md` | move + header; full rewrite in Prompt 02 |
| `modules/pos/SPEC.md` | `_archive/specs-legacy/pos-SPEC.md` | archive (Tier 4 — deferred to Phase 4) |
| `modules/fleet/SPEC.md` | `_archive/specs-legacy/fleet-SPEC.md` | archive |
| `modules/inventory/SPEC.md` | `_archive/specs-legacy/inventory-SPEC.md` | archive |
| `modules/surveys/SPEC.md` | `_archive/specs-legacy/surveys-SPEC.md` | archive |
| `modules/cms/SPEC.md` | `_archive/specs-legacy/cms-SPEC.md` | archive |

### 1.4 Decisions / ADR inventory

All 14 ADRs (+ 2 amendments) stay in `docs/decisions/` but will be renumbered to the 4-digit
`NNNN-kebab-title.md` format by Phase 2 Agent C. Status values are preserved.

| Current | Target (Phase 2 renumbering) | Status |
|---------|-----------------------------|--------|
| ADR-001-modular-monolith.md | `0001-modular-monolith.md` | Accepted |
| ADR-002-multi-tenancy.md | `0002-schema-per-tenant.md` | Accepted |
| ADR-003-deployment-strategy.md | `0003-deployment-strategy.md` | Accepted |
| ADR-004-centralized-permission-seeding.md | `0004-centralized-permission-seeding.md` | Accepted |
| ADR-005-transactional-outbox-pattern.md | `0005-transactional-outbox.md` | Accepted |
| ADR-005-amendment-1.md | merge into 0005 as appended amendment section | Accepted |
| ADR-006-permission-based-authorization.md | `0006-permission-based-authorization.md` | Accepted |
| ADR-007-tab-based-layout-standard.md | `0007-tab-based-layout.md` | Accepted |
| ADR-008-gdpr-deletion-strategy.md | `0008-gdpr-deletion-strategy.md` | Accepted |
| ADR-009-audit-module-repository-pattern.md | `0009-audit-repository-pattern.md` | Accepted |
| ADR-010-notification-delivery-kafka.md | `0010-notification-delivery-kafka.md` | Accepted |
| ADR-011-outbox-service-atomicity.md | `0011-outbox-service-atomicity.md` | Accepted |
| ADR-011-addendum-1.md | append to 0011 | Accepted |
| ADR-012-tenant-management-decisions.md | `0012-tenant-management.md` | Accepted |
| ADR-013-cache-cross-instance-invalidation.md | `0013-cache-cross-instance-invalidation.md` | Accepted |
| ADR-014-distributed-consistency-patterns.md | `0014-distributed-consistency-patterns.md` | Accepted |
| **ADR-015-roadmap-structure.md** (new, Phase 1) | `0015-roadmap-structure.md` | **Proposed** |
| **ADR-016-module-tier-classification.md** (new, Phase 1) | `0016-module-tier-classification.md` | **Proposed** |
| **ADR-017-portal-extension-architecture.md** (new, Phase 1) | `0017-portal-extension-architecture.md` | **Proposed** |

### 1.5 Standards inventory

| Current | Target | Action |
|---------|--------|--------|
| `standards/CODING_STANDARDS.md` | `standards/code-style.md` | split into code-style.md + architectural-principles.md + testing.md |
| `standards/API_INTEGRATION_STANDARDS.md` | merged into `standards/code-style.md` backend API section | consolidate |
| `standards/CODE_REVIEW_STANDARDS.md` | `standards/code-review.md` | rename |
| `standards/CONSISTENCY_STANDARDS.md` | referenced from `0014-distributed-consistency-patterns.md` | fold into ADR; keep link |
| `standards/DOCUMENTATION_STANDARDS.md` | `standards/documentation-style.md` | rename |
| `standards/FRONTEND_STANDARDS.md` | split: `code-style.md` (TS section) + new UX standard (see below) | split |
| `standards/INFRASTRUCTURE_STANDARDS.md` | `standards/architectural-principles.md` (infra parts) | fold |
| `standards/LOCALIZATION_STANDARDS.md` | `standards/localization.md` | rename |
| `standards/OBSERVABILITY_STANDARDS.md` | `standards/architectural-principles.md` (observability section) + keep as satellite | fold + link |
| `standards/RELEASE_STANDARDS.md` | `standards/commit-style.md` + release section | split |
| `standards/UX_UI_STANDARDS.md` | keep as `standards/ux-ui.md` | rename |
| *(new)* `standards/permissions.md` | new file | Agent B writes |
| *(new)* `standards/audit-coverage.md` | new file | Agent B writes |
| *(new)* `standards/multi-currency.md` | new file | Agent B writes |
| *(new)* `standards/security-review.md` | new file | Agent B writes |
| *(new)* `standards/testing.md` | extracted from CODING_STANDARDS | Agent B writes |

---

## 2. Legacy `docs/roadmap/ROADMAP.md` — Open Items Extraction

Items with `[ ]` (not done) from ROADMAP.md grouped by their target new-phase file.

### 2.1 → `phases/phase-1.5-bridge.md` (in progress)

- **1.5.2** Separate Platform Admin role from tenant-scoped roles *(deferred to NMP)*
- **1.5.2** Tenant admin scope enforcement (documentation-only; already enforced by schema isolation)
- **1.5.2** License-based limits (max users, max orgs) *(deferred to NMP)*
- **1.5.3** US tax receipt template + TR bağış makbuzu template *(deferred to Phase 3 Fundraising)*
- **1.5.6** Contact Module enhancements (5 items):
  - User ↔ Contact linking (optional `ContactId?` on User; design with CRM)
  - Contact Import field mapping wizard
  - Contact Export improvements (custom fields, date range, vCard)
  - GDPR Hard Delete (Article 17 — physical removal plan, cross-module cleanup event)
- **1.5.7** Demo Data Framework (all 5 items): `SeedDemoData()`, `nexora demo:load`, demo scenarios (General + NGO), admin UI, cleanup command

### 2.2 → `phases/phase-2-enterprise.md`

- **2.1 CRM** (13 open items) — lead mgmt, pipelines + templates, activities, lead sources, segmentation, email/SMS marketing, campaigns, call center, mobile views, analytics, public lead capture forms
- **2.2 Finance** (11 open items) — income/expense tracking, bank accounts, bank import/reconciliation, budgets, multi-currency, reports, auto-record from other modules, QuickBooks/Xero sync, stakeholder portal
- **2.3 Subscription & Billing** (11 open items) — plans, cycles, auto-invoice, Stripe/iyzico, reminders, overdue, discounts, multi-currency, proration, revenue recognition, payment portal
- **2.4 Projects** (13 open items) — projects, Kanban tasks, time tracking, teams, budgets, cost centers, meetings, subcontractor contracts, dashboards, Gantt, finance integration, stakeholder portal
- **2.5 Reporting enhancements** (6 open items) — templates, email delivery, SQL autocomplete, visual query builder, public sharing, versioning

### 2.3 → `phases/phase-2.5-hr.md`

- **3.4 HR & Payroll** (10 open items) — employee records, departments, contracts, payroll runs, leave mgmt, attendance, shifts, personnel docs, self-service portal, Gusto/ADP/BambooHR sync

### 2.4 → `phases/phase-3a-ngo.md`

- **4.4 Fundraising** (20 open items) — donation categories, one-time/recurring, cart, Stripe/iyzico/Param, multi-currency, auto receipts, donor matching, guest donations, bank import, video/media, Zakat calculator, page builder, campaigns/crowdfunding, collection points, reports, finance integration, donor portal
- **4.5 Sponsorship & Programs** (8 open items) — program mgmt, sponsor-beneficiary matching, installment plans, payment reminders, progress updates, aid distribution, reports, sponsor portal
- **3.2 Events (NGO)** (10 open items) — event creation (generic), categories/templates, registration/ticketing, QR check-in, venue/speaker, sponsor history, calendar, calendar sync, reports, public event pages

### 2.5 → `phases/phase-3b-education.md`

- **4.6 Education** (12 open items) — academic year, grade levels/classrooms/students, enrollment pipeline, guardian linking, appointments, staff availability, academic calendar, accreditation tracking, summer camp, student docs, waitlist, parent portal

### 2.6 → `phases/phase-4-extensions.md`

- **4.1 Accounting advanced** (12 open items) — COA, double-entry, fiscal years, bank mgmt, reconciliation, expense approval, budget variance, multi-currency, tax rates, consolidated reports, auto-journaling, QB/Xero advanced
- **4.2 POS** (12 open items) — sales screen, sessions, catalog, payments, receipts, cash mgmt, EoD reconciliation, event POS, offline, inventory integration, accounting integration, Square sync
- **4.3 Fleet** (10 open items) — vehicles, assignment, insurance, maintenance scheduling+records, fuel logging, inspections, documents, cost tracking, dashboards
- **3.1 CMS** (13 open items) — multi-site, page builder, blog, SEO, form builder, themes, media library, navigation, redirects, multi-language, mobile responsive, live chat, self-service
- **3.3 Surveys** (9 open items) — builder, sections/branching, distribution, anonymity, real-time, analytics, templates, recurring, export
- **3.5 Inventory** (10 open items) — warehouses, locations, catalog, movements, assets, assignment, stocktake, alerts, suppliers, reports

### 2.7 → `phases/phase-4-extensions.md` (Cross-phase continuous)

- Performance / load testing
- Security audits & pen testing
- WCAG 2.1 AA accessibility
- User + API + developer docs
- Automated testing expansion
- Portal UI per vertical release
- Additional locales
- 3rd-party connectors
- Remove Infrastructure dependency from Contacts unit tests (technical debt)
- Mobile app (React Native)
- Marketplace (3rd party modules)

### 2.8 → `phases/phase-NMP-track.md` (separate NMP track — parallel to Phase 2)

- NMP.1 Foundation (6 items)
- NMP.2 Billing integration (4 items)
- NMP.3 Admin panel adaptation (5 items)
- NMP.4 On-prem & marketplace (7 items)

*(Decision: NMP may live as its own track file alongside phase-2; Agent A picks final home.)*

### 2.9 Satellite roadmap docs

- `docs/roadmap/OUTBOX_INBOX_PATTERN_PLAN.md` → `phases/phase-1.5-bridge.md` (as implementation-plan link; file remains in `_archive/roadmap-legacy-plans/` or stays in roadmap as an implementation plan — Agent A to decide)
- `docs/roadmap/SOFT_DELETE_MIGRATION_PLAN.md` → already executed; move to `_archive/roadmap-legacy-plans/SOFT_DELETE_MIGRATION_PLAN.md`

---

## 3. Action Plan Summary (≈ 20 items for Phase 2 agents)

1. Agent A — write 7 phase files (phase-1, 1.5, 2, 2.5, 3a, 3b, 4)
2. Agent A — write `roadmap/README.md` + `phases/README.md` + confirm `current.md`
3. Agent A — migrate 10 open Phase 1.5 items into `analysis/tasks/phase-1.5/T-NNN.md`
4. Agent A — write `analysis/tasks/README.md` + `TEMPLATE.md`
5. Agent A — write `analysis/reviews/business-reviews/master-plan.md`
6. Agent A — audit `.claude/skills/`; add 7 missing skills
7. Agent B — write 13 standard docs (split + new)
8. Agent C — write `decisions/README.md` + `template.md`
9. Agent C — renumber and normalize 14 existing ADRs to `NNNN-kebab.md` format
10. Agent C — place 3 Phase-1 foundation ADRs (Proposed) under the new numbering scheme
11. Agent D — create 4 tier folders
12. Agent D — move 9 Tier-1/2 specs (with Tier headers)
13. Agent D — merge donations + sponsorship into `tier-3a-ngo/fundraising/`
14. Agent D — move events + education into Tier-3 folders
15. Agent D — create stub specs for `portal-framework` and `admin-dashboard`
16. Agent D — archive Tier-4 specs (POS, Fleet, Inventory, Surveys, CMS) to `_archive/specs-legacy/`
17. Agent E — write `architecture/README.md`, `overview.md`, `multi-tenancy.md`, `portal-extensions.md`
18. Agent E — copy legacy ROADMAP.md → `_archive/roadmap-legacy.md` (original kept; deletion is a separate maintainer task)
19. Agent E — stub `guides/README.md`, `audits/README.md`
20. Agent E — finalize this `_archive/migration-notes.md` (add execution log)

**Maintainer-only actions (not performed by any agent):**

- Promote the 3 Phase-1 ADRs from `Proposed` → `Accepted`.
- Delete the original `docs/roadmap/ROADMAP.md` once the archive copy is verified.
- Promote task statuses from `In Review` → `Done`.

---

## 4. Constraints & Non-Goals (Phase 1–2)

- **Spec content is NOT revised** in Phase 1 or Phase 2. All content rewrites (NGO example cleanup,
  accounting → finance B2B revision, subscription spec rewrite, donations + sponsorship merge body)
  are Prompt 02 work.
- **No deletions.** Every legacy file is preserved either in place or in `_archive/`.
- **Frontend code and product code are untouched.** Phase 1/2 only rearrange `docs/` and
  `.claude/skills/`.

---

## 5. Phase 2 Execution Log — Agent E

**Date:** 2026-04-22

### Files created
- `docs/architecture/README.md` — index of architecture docs.
- `docs/architecture/overview-concise.md` — concise big-picture overview.
  **Note:** intended name `overview.md` collides with existing `OVERVIEW.md` on this
  case-insensitive APFS volume; landed as `overview-concise.md` pending maintainer rename.
- `docs/architecture/multi-tenancy.md` — schema-per-tenant deep dive.
- `docs/architecture/portal-extensions.md` — long-form derivative of ADR-017.
- `docs/_archive/README.md` — archive policy.
- `docs/_archive/roadmap-legacy.md` — verbatim copy of `docs/roadmap/ROADMAP.md` with the
  required 5-line archival header prefix.
- `docs/guides/README.md` — guides index with placeholders.
- `docs/audits/README.md` — stub for future audit reports.

### Copies placed in `_archive/` (originals retained)
- `docs/roadmap/OUTBOX_INBOX_PATTERN_PLAN.md` → `docs/_archive/roadmap-legacy-plans/OUTBOX_INBOX_PATTERN_PLAN.md`
- `docs/roadmap/SOFT_DELETE_MIGRATION_PLAN.md` → `docs/_archive/roadmap-legacy-plans/SOFT_DELETE_MIGRATION_PLAN.md`

All originals remain in place; deletion is a maintainer-only action (see §3 maintainer-only
actions list).

---

## 6. Post-Phase-2 Maintainer Cleanup — 2026-04-22

Performed by the orchestrating agent with explicit user approval:

- **Deleted** `docs/roadmap/OUTBOX_INBOX_PATTERN_PLAN.md` and `docs/roadmap/SOFT_DELETE_MIGRATION_PLAN.md`. Both implementations have shipped (outbox in Phase 1.5.1, soft-delete in Phase 0); verbatim copies remain in `_archive/roadmap-legacy-plans/`.
- **Archived 11 legacy UPPERCASE standards files** from `docs/standards/` → `docs/_archive/standards-legacy/` (README.md added listing replacements). Lowercase replacements in `docs/standards/` are canonical.
- **Archived** `docs/architecture/OVERVIEW.md` → `docs/_archive/architecture-legacy/OVERVIEW.md`. Agent E's `overview-concise.md` renamed to canonical `architecture/overview.md`; `architecture/README.md` and the file's own header updated.
- `docs/roadmap/ROADMAP.md` **not yet deleted** — denied by permission gate as maintainer-only action. Archive copy exists at `_archive/roadmap-legacy.md`.

Remaining maintainer-only actions:
- Delete `docs/roadmap/ROADMAP.md` when ready.
- Promote Phase 2 agent outputs from `In Review` → `Done`.

