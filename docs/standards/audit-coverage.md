# Audit Coverage Standard

Derives from: [ADR-009 Audit Repository Pattern](../decisions/ADR-009-audit-module-repository-pattern.md).
Cross-refs: [ADR-008 GDPR Deletion Strategy](../decisions/ADR-008-gdpr-deletion-strategy.md),
[security-review.md](security-review.md) §7.

The Audit module records an append-only history of security- and compliance-relevant
operations across every Nexora module. This standard defines **what each module MUST audit**
and how the baseline matrix is classified.

## 0. Quick Rules

- **MUST AUDIT** — any financial mutation; user create/delete; permission grant/revoke;
  role membership change; tenant settings change; platform admin actions; secret rotation;
  MFA enable/disable; impersonation start/stop.
- **SHOULD AUDIT** — CRM lead / opportunity / campaign state changes; document access
  (download of restricted files, share-link creation); bulk export; bulk import; merge
  / split; permission change on an individual resource (e.g. a document's ACL).
- **MAY AUDIT** — individual read requests, UI navigation, non-sensitive lookups. Opt-in
  per module; default off to keep volume manageable.

Rule of thumb: if a breach investigator or regulator would ask "who did this and when?"
the action MUST be audited. If omitting it would embarrass the compliance officer, it
SHOULD be audited.

## 1. Classification Levels

| Level | Meaning |
|-------|---------|
| **MUST** | The module MUST emit an audit event for this operation class. Missing coverage is a CRITICAL review finding. |
| **SHOULD** | The module SHOULD emit an audit event; omission requires a comment explaining why. |
| **MAY** | Audit at the module's discretion; often volume-sensitive or low-sensitivity reads. |

## 2. Operation Classes

| Class | Examples |
|-------|----------|
| `create` | Entity creation (user, lead, donation, document) |
| `update` | State mutation (status change, field edit, reassignment) |
| `delete` | Soft delete, hard delete, GDPR erasure, uninstall cleanup |
| `read-sensitive` | Export, bulk report, PII view, financial report, audit-log view |
| `security-event` | Login success/fail, permission grant/revoke, role change, MFA change, impersonation, tenant admin action, secret rotation |

`read-sensitive` is **not** every GET request — only the reads that a breach investigator
would want reconstructed (bulk exports, PII disclosure, admin-initiated views of another
user's data).

## 3. Baseline Matrix

This matrix is the **minimum** — modules may exceed it.

| Module | create | update | delete | read-sensitive | security-event |
|--------|:------:|:------:|:------:|:-------------:|:--------------:|
| **Identity** | MUST | MUST | MUST | MUST (export users, role list export) | MUST (login, logout, failed auth, permission change, role change, MFA change, impersonation) |
| **Contacts** | MUST | MUST | MUST | MUST (export, PII view of another org's contact) | **MUST** (merge, split, `contacts:gdpr_erasure` — both anonymize and hard-delete paths write an append-only `GdprErasureAudit` record; see T-004 / ADR-0008) |
| **Documents** | MUST | MUST | MUST | MUST (download of restricted doc, share-link creation) | SHOULD (permission change on doc) |
| **Notifications** | SHOULD (template create) | SHOULD (template edit) | SHOULD (template delete) | MAY | MUST (delivery failure to external provider, mass-send authorization) |
| **Audit** | n/a (self) | n/a | n/a | MUST (any read of the audit log) | MUST (retention purge, admin query) |
| **Reporting** | SHOULD (report create) | SHOULD (report edit) | SHOULD | MUST (report execution if report returns PII / financial data) | MAY |
| **Portal Framework** | SHOULD (layout / content-block create) | SHOULD | SHOULD | MAY | MUST (portal-user identity linkage change, public-endpoint exposure toggle) |
| **Admin Dashboard** | MAY (widget create) | MAY | MAY | MAY | MUST (tenant settings change, feature flag toggle, module install/uninstall) |
| **CRM** (Tier 2) | MUST (lead, opportunity, campaign) | MUST | MUST | SHOULD (bulk export) | MAY |
| **Finance** (Tier 2) | MUST | MUST | MUST | MUST (ledger export, financial report run) | MUST (reconciliation approve, journal post) |
| **Subscription** (Tier 2) | MUST | MUST | MUST | SHOULD (invoice export) | MUST (plan change, payment success/failure, refund) |
| **Projects** (Tier 2) | MUST | MUST | MUST | MAY | MAY |
| **HR** (Tier 2.5) | MUST | MUST | MUST | MUST (payroll export, personnel file view) | MUST (salary change, contract change, termination) |
| **Fundraising** (Tier 3a) | MUST | MUST | MUST | MUST (donor list export, donation report) | MUST (refund, payment-gateway event, receipt regeneration) |
| **Events (NGO)** (Tier 3a) | MUST | MUST | MUST | SHOULD (attendee export) | MAY |
| **Education** (Tier 3b) | MUST | MUST | MUST | MUST (student-data export, grade report) | MUST (enrollment status change, guardian change) |

> Modules listed at Tier 2+ follow the same rules once implemented. Modules archived under
> `_archive/specs-legacy/` (POS, Fleet, Inventory, Surveys, CMS) will re-enter this matrix
> when they return to the roadmap.

## 4. Event Payload Contract

Per [ADR-009](../decisions/ADR-009-audit-module-repository-pattern.md), every audit event
carries:

```
{
  "tenantId":    "<tenant GUID>",
  "organizationId": "<org GUID | null>",
  "actor":       { "userId": "...", "displayName": "...", "impersonatedBy": null },
  "module":      "donations",
  "resource":    "donation",
  "resourceId":  "don-123",
  "action":      "refund",
  "class":       "security-event",
  "outcome":     "success | failure | denied",
  "correlationId": "00-abc...",
  "occurredAt":  "2026-04-22T14:12:00Z",
  "before":      { ... optional snapshot ... },
  "after":       { ... optional snapshot ... },
  "metadata":    { ... module-specific ... }
}
```

Rules:

- PII MUST be minimized — store IDs, not plaintext (e.g. `contactId`, not the full address).
- The audit store is append-only — no updates, no deletes except the retention purge which
  is itself audited.
- `before`/`after` snapshots are required for `update` events on financial or permission
  data; optional elsewhere.
- `correlationId` matches the request's `CorrelationId` for cross-log pivot.

## 5. Implementation Pattern

Modules write audit events through the repository pattern defined in
[ADR-009](../decisions/ADR-009-audit-module-repository-pattern.md) — typically via
`IAuditRepository.RecordAsync(new AuditEvent(...), ct)` invoked from the command handler
(success path) or the outbox on terminal state (for Tier-3 consistency flows, per
[ADR-014](../decisions/ADR-014-distributed-consistency-patterns.md)).

Rules:

- Never emit audit events from inside a catch-all exception handler — audit on definite
  outcomes only (success, explicit failure, explicit denial).
- For Tier-3 consistency commands, audit in the webhook confirmation step, not at pending
  creation.

## 6. Retention & Access

- Default retention (platform defaults; per-tenant override via `TenantConfiguration`):
  - **7 years** — security events, identity (user / permission / role) changes, all financial
    operations (Finance GL, Subscription invoices/payments, Fundraising donations, HR payroll,
    HR contracts), Education admission decisions + enrollment state changes, and the audit log
    itself.
  - **2 years** — everything else (non-security CRUD, attendance corrections, opt-in reads).
  - Expiry action is **purge** by default; tenants may opt into **anonymize-only** via the
    `nexora/audit/retention-mode` tenant setting (values: `purge` | `anonymize`).
  - Tenant-override keys: `nexora/audit/retention-years-security` (default 7),
    `nexora/audit/retention-years-default` (default 2), `nexora/audit/retention-mode`
    (default `purge`).
- Audit read access itself is `audit.event.view` (platform) or `audit.event.view` (tenant,
  restricted to compliance role). Both are audited.
- Retention purges run as a `NexoraJob` on queue `maintenance`; the purge itself emits a
  `security-event` summarizing what was removed.

## 7. Cross-References

- [permissions.md](permissions.md) — permission changes are MUST-audit security events.
- [security-review.md](security-review.md) §7 — new sensitive-data paths MUST be classified
  here before merge.
- [localization.md](localization.md) — audit messages in UI use `lockey_` keys; the audit
  event itself stores codes, not translated strings.

## CRM

Scope: Tenant. Module: `crm` (Tier 2 — Enterprise). Expands row 7 of the baseline matrix with explicit per-resource coverage.

| Resource \ Operation | create | update | delete | read-sensitive | security-event |
|----------------------|:------:|:------:|:------:|:--------------:|:--------------:|
| `lead` | MUST | MUST (stage change, reassignment, qualify/disqualify) | MUST | SHOULD (bulk export) | MAY |
| `opportunity` | MUST | MUST (stage change, amount change, close) | MUST | SHOULD (bulk export, pipeline value report) | SHOULD (reopen closed opportunity) |
| `pipeline` | MUST | MUST (rename, stage add/remove/reorder, automation rule change) | MUST (archive) | MAY | MAY |
| `pipelineStage` | MUST | MUST | MUST | MAY | MAY |
| `campaign` | MUST | MUST (schedule, cancel, content change) | MUST | SHOULD (recipient list export) | MUST (mass-send authorization — delegated to Notifications; CRM records the authorization event) |
| `activity` | SHOULD | SHOULD | SHOULD | MAY | MAY |
| `customFieldDef` | MUST | MUST | MUST | MAY | MAY |

Notes:

- Kanban / list reads are **MAY** — high-volume UX reads are excluded per §2.
- Stage-change audits include `before`/`after` `{stageId, status}` snapshots (required on `update` of pipeline-affecting state).
- `OpportunityWon` / `OpportunityLost` emit both a domain event (on the outbox) **and** an audit `update` event with `after.status in {won, lost}` and reason code where applicable.
- Campaign send authorization is the security-event; per-recipient delivery outcomes are owned by Notifications' audit coverage, not duplicated here.

## Finance

Scope: Tenant. Module: `finance` (Tier 2 — Enterprise Core). Expands the Finance row of the baseline matrix in §3 with explicit per-operation coverage. All rows written through `IAuditRepository` per [ADR-009](../decisions/0009-audit-repository-pattern.md).

| Operation | Class | Level | Notes |
|-----------|-------|:-----:|-------|
| Journal entry post | security-event | **MUST** | `source_module`, `source_document_id`, per-line debit/credit snapshot in `after`. |
| Journal entry void | security-event | **MUST** | Reversing-entry reference required; `before` snapshot mandatory. |
| COA account create | create | **MUST** | All new GL accounts. |
| COA account update | update | **MUST** | `before`/`after` snapshots mandatory (financial data). |
| COA account deactivate | delete | **MUST** | Deactivation (soft); hard-delete forbidden. |
| Budget create / activate / close | update | **MUST** | Approver ID captured on activation. |
| Reconciliation approve / complete | security-event | **MUST** | Statement date, ending balance, approver, difference captured. |
| Reconciliation match / unmatch | update | SHOULD | Per-transaction adjustments within a batch. |
| Bank statement import | create | SHOULD | Import batch id, source (`ofx`/`csv`/`mt940`), counts. |
| Fiscal period close / reopen / lock | security-event | **MUST** | Lock is irreversible; reopen requires elevated role. |
| Exchange-rate manual override | security-event | **MUST** | Daily snapshot job writes are NOT audited per row (volume); only manual admin overrides. |
| Financial report read | read-sensitive | MAY | Reporting module holds the primary row; Finance emits only when the data view returns PII-joined rows (e.g. payee drilldown). |
| Subscription / HR / POS / Fundraising auto-posting | security-event | **MUST** | Treated identically to a manual journal post; `source_module` distinguishes. |

Notes:

- Inbound integration-event handlers emit the audit row only once they transition an aggregate to `Posted`; pending or rejected inbound events emit an `update` row with `outcome = denied` and reason.
- Report reads are not double-audited — when Reporting already records the execution, Finance suppresses its own row unless a payee/vendor drilldown view is invoked.

## Subscription

Scope: Tenant. Module: `subscription` (Tier 2 — Enterprise Core). Expands the Subscription
row of the baseline matrix in §3 with explicit per-operation coverage. See
`docs/modules/tier-2-enterprise/subscription/SPEC.md` §13.

| Operation | Class | Level | Notes |
|-----------|-------|:-----:|-------|
| Plan create | create | **MUST** | New plans. |
| Plan update | update | **MUST** | `before`/`after` on price, currency, interval, trial, tax rate. |
| Plan activate / deactivate | update | **MUST** | Affects which plans are sellable. |
| Subscription create | create | **MUST** | `contactId`, `planId`, `currency`, `startDate`. |
| Subscription upgrade | security-event | **MUST** | `before.planId`, `after.planId`, `prorationAmount`. |
| Subscription downgrade | security-event | **MUST** | Scheduled effective date recorded; fires again on actual application. |
| Subscription cancel | security-event | **MUST** | `immediate` flag, reason code, effective date. |
| Invoice issue | create | **MUST** | Invoice number, total, currency, due date. |
| Invoice paid | security-event | **MUST** | Provider charge id, amount, currency, `paidAt`. |
| Invoice void | update | **MUST** | Reason required; `before` snapshot mandatory. |
| Payment attempt failed | security-event | **MUST** | Attempt number, provider failure code, provider name. |
| Refund applied (from Finance) | security-event | **MUST** | Links to originating `PaymentAttempt` and `Finance.RefundIssued`. |
| Payment method register | update | **MUST** | Provider, method type, last4 / brand snapshot; never the raw token. |
| Payment method replace / remove | update | **MUST** | `before`/`after` of default flag; removal is soft. |
| Set default payment method | update | SHOULD | `before`/`after` of `default_payment_method_id`. |
| Invoice PDF download | read-sensitive | SHOULD | Only when fetched on behalf of another contact (admin view). |
| Plan / subscription / invoice list read | read-sensitive | MAY | High-volume UX reads excluded per §2. |
| Manual provider override (per ADR-0018) | security-event | **MUST** | TTL, approver, previous provider recorded. |

Notes:

- Portal self-service reads (`/portal/**`) by the resource owner are **MAY** — not audited by
  default; admin-initiated views of another contact's data ARE audited (`read-sensitive`).
- Dunning retries emit one audit row per attempt (class `security-event`) to support
  forensic reconstruction of a charge sequence.
- Per ADR-0018, the manual provider override is a `security-event` with both grant and
  auto-expiry rows audited.

## Fundraising

Module ID: `fundraising`. Tier 3a. Baseline already listed in §3; this section enumerates
the concrete operations the module MUST audit.

| Operation | Class | Level | Notes |
|---|---|:---:|---|
| Create donation (any source) | create | **MUST** | `source` in metadata; `resource = donation` |
| Refund donation | security-event | **MUST** | before/after snapshot required (financial data) |
| Update donation (reclassify category, manual reconciliation) | update | **MUST** | before/after snapshot |
| Create / update / cancel RecurringDonationPlan | create / update | **MUST** | pause/resume also audited as `update` |
| RecurringChargeFailed (terminal — plan marked `PaymentFailed`) | security-event | **MUST** | emitted from webhook outbox, not from retry loop |
| Create / activate / cancel Sponsorship | create / update | **MUST** | activation includes beneficiary assignment |
| Assign / reassign beneficiary to sponsorship | update | **MUST** | links two PII subjects — snapshot both ids |
| Create / update / delete Beneficiary | create / update / delete | **MUST** | — |
| Publish ProgressUpdate (single or bulk) | create | **MUST** | for bulk, one audit event per sponsorship row |
| Export donor list / donation report | read-sensitive | **MUST** | filters + row-count recorded in metadata |
| Regenerate receipt | security-event | **MUST** | tax-relevant; reissue captured |
| Import bank statement / confirm matches | create | **MUST** | batch id in metadata |
| Zakat calculation (anonymous) | — | MAY | volume-sensitive; audit only when `contact_id` is set and a Donation follows |
| Qurban share assignment | update | **MUST** | gated by feature flag |

Retention:

- `security-event` and financial `create`/`update`/`delete`: 7 years.
- `read-sensitive` (exports): 2 years.
- Override via tenant configuration only with an ADR amendment.

Notes:

- Portal self-service reads by the donor on their own records are MAY — not audited by
  default; admin-initiated views of another donor's data ARE audited (`read-sensitive`).
- Recurring charge retries that do **not** terminate the plan are not individually
  audited; only the terminal failure (plan → `PaymentFailed`) is audited, with the
  attempt count in metadata.

---

## Projects

Added 2026-04-22.

| Operation | Classification | Notes |
|-----------|---------------|-------|
| Project create / status change / close / reopen | **MUST** | Capture before/after status, actor, correlation id |
| Budget create / approve / line change | **MUST** | Financial impact |
| Cost-entry create / update | **MUST** | Feeds Finance GL |
| Time-entry delete | **MUST** | Prevents covert billable-hour revision |
| Time-entry create / update | SHOULD | High-volume; sampled acceptable unless tenant policy says MUST |
| Meeting-note → task conversion | SHOULD | |
| Task delete, bulk export | SHOULD | |
| Subcontract create / status change | **MUST** | Contract binding |
| Member add / remove / role change | **MUST** | Access control |
| Read endpoints (Kanban, Gantt, list, dashboard, portal) | MAY | Opt-in per tenant |

---

## HR

Added 2026-04-22.

| Operation | Classification | Notes |
|-----------|---------------|-------|
| Payroll prepare / approve / reject / export | **MUST** | Preparer, approver, four-eyes outcome, period totals |
| Employment contract create / send / activate / terminate / renew | **MUST** | Before/after salary, dates, document id |
| Employee create / terminate / manager rewrite / export | **MUST** | Termination: reason code + effective date |
| Leave approve / reject / balance adjustment | SHOULD | Approver, reason, delta |
| Leave submit / cancel | SHOULD | Standard CRUD audit |
| Attendance correction by admin | SHOULD | Original + corrected values |
| Attendance self check-in/out | MAY | High-volume; sampled unless tenant elevates |
| Attendance read | MAY | Elevated-sensitivity tenants only |
| Shift publish | SHOULD | Shift id + affected count |
| Personnel document upload / archive / retention mark | **MUST** | Inherits Documents audit chain |

---

## Education (Tier 3b)

Added 2026-04-22.

| Operation | Classification | Notes |
|-----------|---------------|-------|
| Admission decision (Accept / Waitlist / Reject) | **MUST** | Immutable record of actor, decision, reason |
| Enrollment state change (Enrolled / Withdrawn / Graduated) | **MUST** | Prior/new state, section, effective date |
| Grade post / report-card publish | **MUST** | Teacher identity, assignment, score; amendments separate |
| Accreditation record create / close | **MUST** | Document reference, responsible staff |
| GuardianLink create / delete | **MUST** | Relation type, pickup flag |
| Student extension payload write (ADR-0020 fields) | **MUST** | PII-sensitive (allergies, medical) |
| Attendance record create / edit | SHOULD | High-volume; full audit on edits |
| Appointment schedule / cancel | SHOULD | |
| Read (list / detail, guardian portal) | MAY | Default off; guardian reads MAY be logged |
