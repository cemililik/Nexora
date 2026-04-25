# Phase 2 — Enterprise Core

**Tier:** 2 — Enterprise Core
**Status:** Not started (next phase; blocked by Phase 1.5 exit bar)
**Planned dates:** 2026-Q2 → 2026-Q4

Phase 2 delivers the Tier-2 generic B2B/SMB modules that every enterprise tenant needs
regardless of vertical: CRM (generic), Subscription & Billing, Finance, and Projects.
HR Core splits out into `phase-2.5-hr.md`. Milestone A pilots the Portal Extension
manifest defined in ADR-0017.

---

## Exit bar

Phase 2 is complete when:

1. CRM, Subscription & Billing, Finance, and Projects modules are in production as Tier-2
   modules per ADR-016 (no Tier-3 or Tier-4 dependencies).
2. Each Tier-2 module publishes a Portal Extension manifest (ADR-0017) and renders in the
   portal via the Phase 1.5.4 slot/tab mechanism.
3. Reporting enhancements (templates, email delivery, SQL autocomplete, visual query
   builder, public sharing, versioning) are live.
4. Architecture tests prove no Tier-2 module imports a Tier-3 or Tier-4 namespace.
5. All four Tier-2 modules have locale parity (en + tr) and Mermaid-diagrammed specs.
6. The Portal Extension pilot module (**CRM**, chosen as pilot on 2026-04-24 —
   see Milestone A for the rationale) reports lessons back into
   `docs/architecture/portal-extensions.md`.

## Scope

### 2.1 CRM (generic)

Generic lead-to-conversion; configurable pipeline templates so the same CRM serves sales,
donor, enrollment, volunteer, and consulting flows without NGO-specific vocabulary.

- Lead management (create, assign, qualify).
- Pipelines / funnels with configurable stages per organization.
- Pre-built pipeline templates (General Sales, Donor, Enrollment, Volunteer, Real Estate,
  Consulting).
- Activities (calls, meetings, tasks linked to leads).
- Lead sources tracking (web form, referral, event, import).
- Segmentation (tags, filters, saved segments).
- Email + SMS marketing integration (via Notifications).
- Campaign management (goals, tracking, attribution).
- Call-center integration (click-to-call).
- Mobile-optimized views for field staff.
- CRM analytics + reports (uses Reporting).
- **Portal:** public lead-capture forms.

### 2.2 Finance

Generic financial tracking for SMBs — standalone today, prerequisite for the advanced
Accounting module in Phase 4.

- Income tracking (payments, invoices, manual entries; categorized).
- Expense tracking (receipts, photo upload, approval workflow).
- Bank account management.
- Bank transaction import (CSV, OFX).
- Basic bank reconciliation.
- Budget management (per category, actual vs. planned).
- Multi-currency support.
- Financial reports (income vs. expense, cash flow, budget variance).
- Auto-record from other modules (Subscription payments, Fundraising donations, POS).
- QuickBooks Online / Xero bi-directional sync.
- **Portal:** financial summary for stakeholders.

### 2.3 Subscription & Billing

Recurring payments for any business model — SaaS, memberships, tuition, retainers.

- Subscription plans (service fees, memberships, tuition, retainers).
- Billing cycles (monthly, quarterly, annual, semester, custom).
- Automatic invoice generation.
- Payment processing (Stripe, iyzico).
- Payment reminders (email, SMS — escalation tiers).
- Overdue tracking + late-fee management.
- Discount / scholarship / coupon management.
- Multi-currency billing.
- Proration + plan changes.
- Revenue recognition reports.
- **Portal:** payment portal (view invoices, pay, download receipts).

### 2.4 Projects

Task and project tracking for internal teams.

- Projects with milestones.
- Kanban tasks with WIP limits.
- Task comments + attachments.
- Time tracking (per task, per member).
- Team / member management.
- Budgeting + cost tracking; cost centers.
- Meeting notes with action-item-to-task conversion.
- Subcontractor contract management (Documents integration).
- Labels + filtering.
- Project dashboard + Gantt.
- Finance integration (cost journal entries).
- **Portal:** stakeholder view (progress, milestones, documents).

### 2.5 Reporting enhancements

Prerequisite: Reporting Engine (Phase 1) + CRM (Phase 2.1).

- Report templates (pre-built SQL per module).
- Email delivery for on-demand reports.
- SQL autocomplete (tenant schema metadata suggestions).
- Visual query builder (Metabase-style).
- Report sharing (public token-based links).
- Report versioning (rollback to previous versions).

## Out of scope

- HR Core — Phase 2.5.
- NGO / Education edition modules — Phase 3a / 3b.
- Tier-4 extensions (POS, Fleet, Inventory, Surveys, CMS) — Phase 4.
- Advanced Accounting (COA, double-entry, fiscal years) — Phase 4.
- NMP billing and license enforcement — parallel NMP track.

## Milestones

### Milestone A — Portal Extension pilot (ADR-0017) + Migration runner foundation + Uninstall contract

**Pilot module: CRM** (selected 2026-04-24 per the authority granted in ADR-0017
"First pilot"). The module pilots the full Portal Extension manifest: tier
declaration, slot contributions (at minimum: tab into Contacts 360° for
lead/activity history, dashboard sidebar widget for "Recent leads"),
permission wiring, locale namespace, marketplace-compatible manifest shape.
Tracked as [T-030](../../analysis/tasks/phase-2/T-030.md).

**Why CRM over Subscription:** CRM exercises more of the ADR-0017 surface —
it contributes into multiple host slots (Contacts 360° tabs, dashboard widgets,
nav entries) whereas Subscription is largely self-contained around a payments
portal. A richer contribution footprint produces more lessons for the
retrospective. CRM also sits on the critical path for Phase 3a (donor
pipelines) and Phase 3b (enrollment pipelines), so early delivery unblocks
downstream phases. Subscription's entanglement with external payment providers
(Stripe + iyzico + proration + revenue-recognition) would couple the pilot
outcome to payment-gateway state — a weaker test of the manifest itself.

Outcomes inform `docs/architecture/portal-extensions.md`.

**Bundled with Milestone A** (because each is a Phase 1.5 hand-off whose
prerequisites are now met or because they share the CRM-pilot delivery
window and depend on one another):

- [T-010](../../analysis/tasks/phase-2/T-010.md) — Cross-module audit payload PII
  scan scaffolding (Contacts locator + Audit scan job + arch test). **CRM
  ships the first non-trivial locator inside this milestone** so the
  contract is exercised end-to-end. 10M-row perf benchmark deferred to
  Milestone C. Reclassified from Phase 1.5 in the 2026-04-24 carry-over
  sweep; unblocked by [ADR-0026](../../decisions/0026-cross-module-pii-payload-scan-for-gdpr-erasure.md).
- [T-011](../../analysis/tasks/phase-2/T-011.md) — `MigrationRunner.MigrateAllModulesAsync`
  + per-tenant `pg_advisory_lock` + dependency-ordered application. Required
  before any Phase-2 module ships its first EF migration. Unblocked by
  [ADR-0027](../../decisions/0027-production-schema-migration-strategy.md).
- [T-012](../../analysis/tasks/phase-2/T-012.md) — `AdditiveOnlyMigrationTest`
  CI gate. Pairs with T-011 because the additive-only rule is the gate
  ADR-0027 is built around.
- [T-025](../../analysis/tasks/phase-2/T-025.md) — `platform:purge-uninstalled-modules`
  Hangfire cleanup job (closes ADR-0028's retention promise).
- [T-026](../../analysis/tasks/phase-2/T-026.md) — Cascade guard in
  `UninstallModuleCommand` — per-module transactions + forward-log
  compensation per [ADR-0031](../../decisions/0031-cascade-uninstall-per-module-transactions.md)
  (which supersedes ADR-0028's outer-transaction model).
- [T-027](../../analysis/tasks/phase-2/T-027.md) — GDPR erasure escape hatch
  for renamed uninstall tables (module-local scan).

### Milestone B — Tier-2 modules shipped

All four Tier-2 modules (CRM, Finance, Subscription, Projects) at feature-complete for
their Phase 2 scope, each using the manifest pattern from Milestone A.

### Milestone C — Reporting enhancements + hardening

§2.5 items; contract tests; architecture tier-boundary tests green; locale parity audit.

## Acceptance criteria

- [ ] CRM, Finance, Subscription, Projects specs promoted to Accepted module specs in
      `docs/modules/tier-2-enterprise/*/SPEC.md`.
- [ ] All four modules ship portal UI via ADR-0017 manifest.
- [ ] Tier-boundary architecture test passes (no Tier-2 → Tier-3/4 references).
- [ ] Reporting templates available for each Tier-2 module.
- [ ] Locale files (en + tr) at parity for all four modules.
- [ ] Pilot retrospective merged into `architecture/portal-extensions.md`.

## ADR ledger

**Introduces:**

- *(none planned at phase-open; new ADRs may emerge from the Milestone A pilot.)*

**Consumes:**

- [ADR-0001](../../decisions/0001-modular-monolith.md) … [ADR-0014](../../decisions/0014-distributed-consistency-patterns.md) — all Phase-1 foundations.
- [ADR-0015](../../decisions/0015-roadmap-structure.md) — Roadmap Structure (phase model).
- [ADR-0016](../../decisions/0016-module-tier-classification.md) — Module Tier Classification (Tier-2 rules).
- [ADR-0017](../../decisions/0017-portal-extension-architecture.md) — Portal Extension Architecture (manifest pilot in Milestone A; tracked as [T-030](../../analysis/tasks/phase-2/T-030.md)).
- [ADR-0018](../../decisions/0018-payment-provider-strategy.md) — Payment Provider Strategy (Subscription module dependency).
- [ADR-0019](../../decisions/0019-recurring-billing-vs-recurring-donations.md) — Recurring Billing vs Recurring Donations (Subscription scope boundary).
- [ADR-0020](../../decisions/0020-contact-extensions-by-vertical-modules.md) — Contact Extensions by Vertical Modules (Tier-3 attachment pattern; consumed by Tier-2 SPECs that draw a vocabulary line).
- [ADR-0021](../../decisions/0021-money-and-exchange-rate-contract.md) — Money + Exchange Rate Contract (every Tier-2 module that touches money).
- [ADR-0022](../../decisions/0022-two-tier-locale-resolution.md) — Two-Tier Locale Resolution (i18n surface for all Tier-2 manifests).
- [ADR-0023](../../decisions/0023-nmp-billing-model.md) — NMP Billing Model (license gates the Tier-2 SKU).
- [ADR-0026](../../decisions/0026-cross-module-pii-payload-scan-for-gdpr-erasure.md) — Cross-Module PII Payload Scan for GDPR Erasure (Milestone A scaffolding via [T-010](../../analysis/tasks/phase-2/T-010.md); CRM is the first non-trivial locator consumer).
- [ADR-0027](../../decisions/0027-production-schema-migration-strategy.md) — Production Schema-Migration Strategy (gates every Tier-2 module's first migration; Milestone A delivers via [T-011](../../analysis/tasks/phase-2/T-011.md) + [T-012](../../analysis/tasks/phase-2/T-012.md)).
- [ADR-0028](../../decisions/0028-module-uninstall-data-retention-contract.md) — Module Uninstall Data-Retention Contract (Milestone A delivers via [T-025](../../analysis/tasks/phase-2/T-025.md) / [T-026](../../analysis/tasks/phase-2/T-026.md) / [T-027](../../analysis/tasks/phase-2/T-027.md)).
- [ADR-0029](../../decisions/0029-cap-blocked-short-circuits-lower-layers.md) — `cap.blocked` short-circuits lower layers in the compliance-config resolver (every Tier-2 module that reads compliance-gated config inherits this semantic).
- [ADR-0030](../../decisions/0030-license-hot-reload-mechanism.md) — License Hot-Reload Mechanism ([T-014](../../analysis/tasks/phase-2/T-014.md) + [T-015](../../analysis/tasks/phase-2/T-015.md)).
- [ADR-0031](../../decisions/0031-cascade-uninstall-per-module-transactions.md) — Cascade Uninstall Uses Per-Module Transactions With Forward-Log Compensation (governs [T-026](../../analysis/tasks/phase-2/T-026.md)'s transaction model; partially supersedes ADR-0028).

**Supersedes:** none.

## Informs

- `phase-2.5-hr.md` — HR Core inherits the Tier-2 + ADR-0017 manifest patterns.
- `phase-3a-ngo.md` — Fundraising depends on Finance (donation → income record) and CRM
  (donor pipeline templates).
- `phase-3b-education.md` — Education depends on Subscription (tuition) and CRM
  (enrollment pipeline).
- `phase-4-extensions.md` — POS and Inventory depend on Finance auto-record.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §2.
- Migration plan: `../../_archive/migration-notes.md` §2.2.
