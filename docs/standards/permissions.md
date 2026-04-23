# Permissions Standard

Derives from: [ADR-004 Centralized Permission Seeding](../decisions/ADR-004-centralized-permission-seeding.md),
[ADR-006 Permission-Based Authorization](../decisions/ADR-006-permission-based-authorization.md).

Nexora uses **permission-based RBAC** — roles are collections of permissions, and
endpoints/components check permissions directly. Roles have no hard-coded semantics beyond
grouping.

## 0. At a Glance

- **Format:** `{module}.{resource}.{action}` — exactly three dot-separated, lowercase tokens (§1).
- **Scope:** every permission is declared as either **Platform** (NMP staff) or **Tenant** (end-user). The two registries are disjoint; cross-scope assignment is rejected at seed time (§2).
- **Declaration:** modules register their permissions in `IModule.OnStartupAsync` via `IPermissionRegistry`; unregistered names cannot be enforced (§3).
- **Built-in roles** (seeded on tenant/platform creation):
  - **Platform Admin** — all `nmp.*` permissions. No tenant permissions.
  - **Tenant Admin** — every tenant permission for every installed module (superset role; automatically picks up new permissions at module install time).
  - **Tenant User** — baseline read + self-service write (`.read` on installed modules plus own-entity `.write` where applicable). No admin, no destructive actions.
  - **Portal User** — customer/member-facing role for the public portal; restricted to portal permissions and read-only access to their own records (e.g., `contacts.contact.read` gated by `ContactId == self`).
- **Default grants:** every module spec's Permission Matrix (§5) MUST list which of the four built-in roles receives each permission on install. The Identity module's seeder reads that matrix during `ModuleInstalled` processing.

## 1. Permission Naming

```text
{module}.{resource}.{action}
```

| Part | Rule | Examples |
|------|------|----------|
| `{module}` | Module name, lowercase, single token | `identity`, `contacts`, `crm`, `donations` |
| `{resource}` | Noun, singular or plural matching DB table convention | `user`, `role`, `lead`, `donation`, `campaign` |
| `{action}` | Verb from the closed set `read \| write \| delete \| admin` | `read`, `write`, `delete`, `admin` |

Examples:

```text
identity.user.read
identity.user.write
identity.role.admin
contacts.contact.read
crm.lead.write
crm.pipeline.admin
donations.donation.admin
donations.campaign.read
```

### Rules

- Single dot between each part. Exactly 3 parts.
- Lowercase. Hyphen allowed inside a part only when the table name itself is hyphenated
  (rare). Prefer a single-word resource name.
- `{action}` is restricted to the closed set **`read | write | delete | admin`**.
  - `read` — view, list, export (read-only operations).
  - `write` — create or update (mutating operations on the resource).
  - `delete` — delete/archive.
  - `admin` — destructive / overriding operations (merge, force-cancel, GDPR erasure, etc.) and resource-level superset.
- Custom sub-resource actions are not part of the normative naming — if a verb (e.g. "refund", "convert") does not fit the closed set, model it as a distinct sub-resource (`donations.refund.write`) rather than a custom action on the parent.

## 2. Scope: Platform vs Tenant

| Scope | Who holds it | Where it is stored | Examples |
|-------|-------------|--------------------|----------|
| **Platform** | Nexora Management Portal (NMP) staff only | `public` schema — shared across all tenants | `nmp.tenant.create`, `nmp.license.manage`, `nmp.module.install` |
| **Tenant** | Tenant users assigned via roles | Tenant schema — isolated per tenant | `crm.lead.view`, `donations.donation.refund` |

Platform permissions are **never** assignable to a tenant-scope role; tenant permissions are
**never** granted to platform-scope roles. Enforced by the permission registry at seed time.

## 3. Declaration: Module Migration Seed

Per [ADR-004](../decisions/ADR-004-centralized-permission-seeding.md), every module
declares its permissions in its own migration seed, and the Identity module's permission
registry collates them on startup.

```csharp
public sealed class CrmModule : IModule
{
    public async Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct)
    {
        registry.Register("crm.lead.read",     scope: PermissionScope.Tenant);
        registry.Register("crm.lead.write",    scope: PermissionScope.Tenant);
        registry.Register("crm.lead.delete",   scope: PermissionScope.Tenant);
        registry.Register("crm.lead.admin",    scope: PermissionScope.Tenant);
        registry.Register("crm.pipeline.read", scope: PermissionScope.Tenant);
        registry.Register("crm.pipeline.admin", scope: PermissionScope.Tenant);
    }
}
```

Rules:

- A permission not registered at startup cannot be enforced — the authorization policy will
  refuse unknown names.
- Removing a permission is a **breaking change** — requires an ADR (supersession) and a
  migration path that strips orphaned role assignments.
- Renaming a permission is forbidden. Add a new permission, deprecate the old one, migrate
  assignments, remove in a later major release.

## 4. Enforcement

### 4.1 Backend — Endpoint

```csharp
[Authorize(Policy = "crm.lead.write")]
public static async Task<IResult> CreateLead(...) { ... }
```

Every HTTP endpoint MUST have either:

- `[Authorize(Policy = "{module}.{resource}.{action}")]`, or
- `[AllowAnonymous]` **with a one-line justification comment** immediately above.

### 4.2 Backend — Command / Query

For operations invoked from background jobs or integration events, use
`IAuthorizationService.AuthorizeAsync(user, "crm.lead.write", ct)` at the handler entry.
See [code-review.md](code-review.md) SEC-9.

### 4.3 Organization Scope

Tenant permissions can be further scoped to an organization. When a permission is granted
against a role that has `OrganizationId`, the authorization handler enforces the `org`
filter on EF queries via the global filter. New entities that are org-scoped MUST include an
`OrganizationId` column and the global filter.

### 4.4 Frontend

Permission checks on the frontend are **UX-only** — they hide UI a user cannot invoke.
Backend is the authority.

```tsx
const { hasPermission } = usePermissions();
if (hasPermission('crm.lead.write')) { /* show button */ }
```

Route guard pattern is defined in `shared/hooks/usePermissions.ts` — do not reimplement.

## 5. Permission Matrix Template

Every module spec (`docs/modules/{module}/SPEC.md` once migrated to
`docs/modules/tier-*/...`) MUST include a **Permission Matrix**. Fill this table when
writing the spec:

```markdown
## Permission Matrix

Scope: Tenant  (or: Platform — for NMP modules)

| Resource \ Action | read | write | delete | admin |
|-------------------|:----:|:-----:|:------:|:-----:|
| <resource-1>      |  ✓   |   ✓   |   ✓    |   ✓   |
| <resource-2>      |  ✓   |   ✓   |   –    |   ✓   |

Legend: ✓ = permission exists, – = not applicable for this resource.
```

Rules for authors:

- Every row and every `✓` corresponds to a permission registered at module startup
  (§3) — if it's in the matrix, the code MUST declare it.
- All four columns use the canonical actions (`read | write | delete | admin`).
- Role presets (suggested role → permission bundle) go in a separate table below the matrix.

### Example — Identity Module (partial)

```markdown
| Resource \ Action | read | write | delete | admin |
|-------------------|:----:|:-----:|:------:|:-----:|
| user              |  ✓   |   ✓   |   ✓    |   ✓   |
| role              |  ✓   |   ✓   |   ✓    |   ✓   |
| organization      |  ✓   |   ✓   |   ✓    |   ✓   |
```

## 6. Testing

- Every permission-gated endpoint has a test for both authorized and unauthorized users
  (see [testing.md](testing.md) §7).
- Every new permission added in a PR includes a test that asserts it is registered
  (`IPermissionRegistry.IsRegistered("...")`).
- Seed-time tests verify no orphaned role-permission rows after a migration.

## 7. Review Checklist Links

- `SEC-9` — permission checks on both ends.
- `SEC-11` — `[Authorize]` or justified `[AllowAnonymous]`.
- `TEST-11` — authenticated and unauthenticated paths tested.
- `TEST-14` — permission guards tested for authorized and unauthorized users.

## 8. Tier-1 Module Permission Matrices

The matrices below are **normative** for Tier-1 Platform Core modules. Tier-2 and Tier-3 modules append their own sections (owned by their respective agents). Legend: ✓ = permission exists; – = not applicable; `PA` Platform Admin, `TA` Tenant Admin, `TU` Tenant User, `PU` Portal User.

### 8.1 Identity (`identity`) — Tenant scope

| Resource | read | write | delete | admin | Default grants |
|----------|:----:|:-----:|:------:|:-----:|----------------|
| `user`         | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read |
| `role`         | ✓ | ✓ | ✓ | ✓ | TA: all |
| `organization` | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read |
| `invitation`   | ✓ | ✓ | ✓ | – | TA: all |
| `session`      | ✓ | – | ✓ | – | TA: all; TU: read+delete own |

Sensitive sub-operations (impersonation, user↔contact linking) are modelled as dedicated sub-resources rather than custom actions — e.g. `identity.impersonation.write` (TA only, audited), `identity.user-contact-link.write` (TA only — links/unlinks a user to a Contacts record; system accounts are always rejected, unlink also triggered automatically on `ContactGdprDeletedIntegrationEvent`).

### 8.2 Contacts (`contacts`) — Tenant scope

> Permissions follow platform convention `{module}.{resource}.{read|write|delete|admin}` — `write` covers both create and update. `admin` covers merge and GDPR anonymize.

| Resource | read | write | delete | admin | Default grants |
|----------|:----:|:-----:|:------:|:-----:|----------------|
| `contact` | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read+write; PU: read own |
| `tag`     | ✓ | ✓ | ✓ | – | TA: all; TU: read |
| `consent` | ✓ | ✓ | – | – | TA: all; TU: read+write own |

### 8.3 Notifications (`notifications`) — Tenant scope

| Resource       | read | write | delete | admin | Default grants |
|----------------|:----:|:-----:|:------:|:-----:|----------------|
| `notification` | ✓ | – | – | ✓ | TA: all; TU: read |
| `template`     | ✓ | ✓ | ✓ | ✓ | TA: all |
| `provider`     | ✓ | ✓ | ✓ | ✓ | TA: all |

`admin` on `notification` covers send (bulk dispatch / override). `admin` on `template`/`provider` covers destructive config overrides.

### 8.4 Documents (`documents`) — Tenant scope

| Resource   | read | write | delete | admin | Default grants |
|------------|:----:|:-----:|:------:|:-----:|----------------|
| `document` | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read+write |
| `folder`   | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read |

Download is implicit in `read`; share is implicit in `admin`.

### 8.5 Audit (`audit`) — Tenant & Platform scopes

| Resource | read | write | delete | admin | Default grants |
|----------|:----:|:-----:|:------:|:-----:|----------------|
| `event`  | ✓ | – | – | ✓ | TA: read; PA: read+admin (platform scope) |

`read` covers view and export; `admin` covers purge.

### 8.6 Reporting (`reporting`) — Tenant scope

| Resource    | read | write | delete | admin | Default grants |
|-------------|:----:|:-----:|:------:|:-----:|----------------|
| `report`    | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read |
| `dashboard` | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read |

`read` covers view and run; `admin` covers schedule and publish.

### 8.7 Portal Framework (`portal`) — Tenant scope

| Resource        | read | write | delete | admin | Default grants |
|-----------------|:----:|:-----:|:------:|:-----:|----------------|
| `profile`       | ✓ | ✓ | – | – | PU: read+write own |
| `layout`        | ✓ | ✓ | ✓ | ✓ | TA: all |
| `content-block` | ✓ | ✓ | ✓ | ✓ | TA: all |

### 8.8 Admin Dashboard (`admin`) — Tenant scope

| Resource | read | write | delete | admin | Default grants |
|----------|:----:|:-----:|:------:|:-----:|----------------|
| `widget`  | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read |
| `layout`  | ✓ | ✓ | ✓ | ✓ | TA: all |
| `setting` | ✓ | ✓ | ✓ | ✓ | TA: all |

> Tier 2 / Tier 3 matrices are appended by their respective module owners. This document remains the canonical index.

## CRM

Scope: Tenant. Module: `crm` (Tier 2 — Enterprise).

Permissions declared at module startup (`CrmModule.OnStartupAsync`):

| Permission | Purpose |
|------------|---------|
| `crm.leads.read` | View leads, lead lists, Kanban. |
| `crm.leads.write` | Create, update, assign, qualify, disqualify, move-stage, archive leads. |
| `crm.leads.delete` | Delete leads. |
| `crm.opportunities.read` | View opportunities and pipeline values. |
| `crm.opportunities.write` | Create / update / move-stage / win / lose opportunities. |
| `crm.opportunities.admin` | Reopen closed opportunities; override assigned owner. |
| `crm.pipelines.read` | View pipeline / stage / custom-field definitions. |
| `crm.pipelines.admin` | Create, rename, archive pipelines; add / reorder / remove stages; manage per-pipeline custom field defs; configure stage-transition automations. |
| `crm.activities.read` | View activities on leads / opportunities / contacts. |
| `crm.activities.write` | Log, edit, complete, reschedule activities. |
| `crm.campaigns.read` | View campaign list, analytics. |
| `crm.campaigns.admin` | Create, edit, schedule, cancel marketing campaigns. |

### Permission Matrix

| Resource \ Action | read | write | delete | admin |
|-------------------|:----:|:-----:|:------:|:-----:|
| leads         | ✓ | ✓ | ✓ | – |
| opportunities | ✓ | ✓ | – | ✓ |
| pipelines     | ✓ | – | – | ✓ |
| activities    | ✓ | ✓ | – | – |
| campaigns     | ✓ | – | – | ✓ |

Role presets (suggested):

- **CRM Viewer** — `crm.leads.read`, `crm.opportunities.read`, `crm.pipelines.read`, `crm.activities.read`, `crm.campaigns.read`.
- **SDR** — CRM Viewer + `crm.leads.write`, `crm.activities.write`.
- **Account Executive** — SDR + `crm.opportunities.write`.
- **RevOps Admin** — Account Executive + `crm.pipelines.admin`, `crm.opportunities.admin`, `crm.campaigns.admin`, `crm.leads.delete`.

## Finance

Scope: Tenant. Module: `finance` (Tier 2 — Enterprise Core).

Permissions declared at module startup (`FinanceModule.OnStartupAsync`):

| Permission | Purpose |
|------------|---------|
| `finance.accounts.read` | View chart of accounts and account balances. |
| `finance.accounts.admin` | Create, update, and deactivate GL accounts and COA structure. |
| `finance.journal.read` | View journal entries and lines. |
| `finance.journal.write` | Create journal entries. |
| `finance.journal.admin` | Approve, post, and void journal entries. |
| `finance.budgets.read` | View budgets and variance reports. |
| `finance.budgets.admin` | Create, edit, activate, and close budgets and budget lines. |
| `finance.bank.read` | View bank accounts and imported bank transactions. |
| `finance.bank.admin` | Import statements, match transactions, and approve reconciliations. |
| `finance.reports.read` | Run Finance-sourced reports (trial balance, income statement, balance sheet, cash flow) via the Reporting module. |

### Permission Matrix

| Resource \ Action | read | write | delete | admin |
|-------------------|:----:|:-----:|:------:|:-----:|
| accounts | ✓ | – | – | ✓ |
| journal  | ✓ | ✓ | – | ✓ |
| budgets  | ✓ | – | – | ✓ |
| bank     | ✓ | – | – | ✓ |
| reports  | ✓ | – | – | – |

Role presets (suggested):

- **Finance Viewer** — `finance.accounts.read`, `finance.journal.read`, `finance.budgets.read`, `finance.bank.read`, `finance.reports.read`.
- **Accountant** — Viewer + `finance.journal.write`, `finance.journal.admin` (post), `finance.bank.admin` (reconcile).
- **Controller** — Accountant + `finance.accounts.admin`, `finance.budgets.admin`.

## Subscription

Scope: Tenant. Module: `subscription` (Tier 2 — Enterprise Core). Declared by the Tier-2
Subscription module; see `docs/modules/tier-2-enterprise/subscription/SPEC.md` §12.

Permissions declared at module startup (`SubscriptionModule.OnStartupAsync`):

| Permission | Purpose |
|------------|---------|
| `subscription.plans.read` | View plan catalog. |
| `subscription.plans.admin` | Create, update, activate / deactivate plans. |
| `subscription.subscriptions.read` | View subscriptions across all contacts. |
| `subscription.subscriptions.write` | Create, upgrade, downgrade, cancel subscriptions. |
| `subscription.subscriptions.admin` | Administrative overrides (backdate, waive proration, force-cancel past `unpaid`). |
| `subscription.invoices.read` | View invoices. |
| `subscription.payment-methods.read` | View payment methods attached to contacts. |
| `subscription.payment-methods.write` | Register, replace, remove payment methods; set default. |

### Permission Matrix

| Resource \ Action | read | write | delete | admin |
|-------------------|:----:|:-----:|:------:|:-----:|
| plans             |  ✓   |   –   |   –    |   ✓   |
| subscriptions     |  ✓   |   ✓   |   –    |   ✓   |
| invoices          |  ✓   |   –   |   –    |   –   |
| payment-methods   |  ✓   |   ✓   |   ✓    |   –   |

Portal self-service endpoints (`/portal/**`) do not require a tenant-wide permission — the
handler scopes queries and mutations to the caller's own `contact_id`.

Role presets (suggested):

- **Billing Viewer** — `subscription.plans.read`, `subscription.subscriptions.read`, `subscription.invoices.read`, `subscription.payment-methods.read`.
- **Billing Operator** — Viewer + `subscription.subscriptions.write`, `subscription.payment-methods.write`.
- **Billing Admin** — Operator + `subscription.plans.admin`, `subscription.subscriptions.admin`.

## 9. Fundraising

Scope: Tenant. Module ID: `fundraising`. Spec:
[docs/modules/tier-3a-ngo/fundraising/SPEC.md](../modules/tier-3a-ngo/fundraising/SPEC.md).

Registered permissions:

```text
fundraising.donations.read
fundraising.donations.write
fundraising.donations.delete
fundraising.donations.admin
fundraising.campaigns.read
fundraising.campaigns.write
fundraising.campaigns.admin
fundraising.recurring.read
fundraising.recurring.write
fundraising.recurring.admin
fundraising.sponsorships.read
fundraising.sponsorships.write
fundraising.sponsorships.admin
fundraising.sponsorship-progress.write
fundraising.beneficiaries.read
fundraising.beneficiaries.write
fundraising.beneficiaries.admin
fundraising.receipts.read
fundraising.receipts.admin
fundraising.qurban.read
fundraising.qurban.write
```

Matrix:

| Resource \ Action | read | write | delete | admin |
|-------------------|:----:|:-----:|:------:|:-----:|
| donations             | ✓ | ✓ | ✓ | ✓ |
| campaigns             | ✓ | ✓ | – | ✓ |
| recurring             | ✓ | ✓ | – | ✓ |
| sponsorships          | ✓ | ✓ | – | ✓ |
| sponsorship-progress  | – | ✓ | – | – |
| beneficiaries         | ✓ | ✓ | – | ✓ |
| receipts              | ✓ | – | – | ✓ |
| qurban *(cap-4)*      | ✓ | ✓ | – | – |

Notes:

- `admin` on `donations` covers refund, export, and import — each is an audited
  security-event (see audit-coverage.md §Fundraising).
- `admin` on `receipts` covers regenerate.
- `fundraising.qurban.*` is gated by the tenant feature flag
  `fundraising.islamic_extensions.enabled` — permissions are always registered, but
  endpoints return `404` when the flag is off.
- Donor portal self-service on own recurring plans / sponsorships is authorised by
  ownership (`plan.DonorContactId == currentUser.ContactId`) **or** the corresponding
  `.write` permission; the handler checks in that order.

Role presets (suggested):

- **Fundraising Viewer** — `fundraising.donations.read`, `fundraising.campaigns.read`,
  `fundraising.recurring.read`, `fundraising.sponsorships.read`,
  `fundraising.beneficiaries.read`, `fundraising.receipts.read`, `fundraising.qurban.read`.
- **Fundraising Operator** — Viewer + `fundraising.donations.write`,
  `fundraising.campaigns.write`, `fundraising.sponsorships.write`,
  `fundraising.sponsorship-progress.write`, `fundraising.beneficiaries.write`,
  `fundraising.recurring.write`, `fundraising.qurban.write`.
- **Fundraising Admin** — Operator + every `fundraising.*.admin` permission and
  `fundraising.donations.delete`.

---

## Projects (`projects.*`) — Tenant scope (organization-filtered)

Added 2026-04-22 by Prompt 3 Projects Agent.

| Permission | Description | PA | TA | TU | PU |
|------------|-------------|:--:|:--:|:--:|:--:|
| `projects.projects.read`       | Read project list / detail | – | ✓ | ✓ | ✓ (own) |
| `projects.projects.write`      | Create / update project | – | ✓ | ✓ | – |
| `projects.projects.admin`      | Close / reopen project, member management | – | ✓ | – | – |
| `projects.tasks.read`          | Read tasks (board, detail) | – | ✓ | ✓ | ✓ (own) |
| `projects.tasks.write`         | Create / update tasks | – | ✓ | ✓ | – |
| `projects.time-entries.read`   | List time entries | – | ✓ | ✓ (own) | – |
| `projects.time-entries.write`  | Log time entries | – | ✓ | ✓ (own) | – |
| `projects.budgets.read`        | View project budget | – | ✓ | ✓ | – |
| `projects.budgets.admin`       | Create / approve / adjust budget | – | ✓ | – | – |
| `projects.cost-entries.read`   | View cost entries | – | ✓ | ✓ | – |
| `projects.cost-entries.write`  | Log cost entries | – | ✓ | ✓ | – |
| `projects.subcontracts.read`   | View subcontracts | – | ✓ | ✓ | – |
| `projects.subcontracts.admin`  | Create / activate / terminate subcontracts | – | ✓ | – | – |

Portal stakeholder routes are gated by a scoped role that intersects `projects.projects.read` + `projects.tasks.read` with the calling contact's linked project list. Project membership management is covered by `projects.projects.admin` (no separate `members` resource).

---

## HR (`hr.*`) — Tenant scope (organization-filtered)

Added 2026-04-22 by Prompt 3 HR Agent.

| Permission | Description | PA | TA | TU | PU |
|------------|-------------|:--:|:--:|:--:|:--:|
| `hr.employees.read`          | Read employee records | – | ✓ | ✓ (HR team) | – |
| `hr.employees.write`         | Create / update employee records | – | ✓ | – | – |
| `hr.employees.admin`         | Terminate, rewrite manager, export | – | ✓ | – | – |
| `hr.contracts.read`          | Read employment contracts | – | ✓ | – | – |
| `hr.contracts.admin`         | Create / amend / terminate contracts | – | ✓ | – | – |
| `hr.leaves.read`             | Read leave requests | – | ✓ | ✓ (approver) | – |
| `hr.leaves.write`            | Create / update leave requests | – | ✓ | ✓ | – |
| `hr.leaves.admin`            | Approve / reject leave requests | – | ✓ | ✓ (approver) | – |
| `hr.payroll.read`            | Read payroll runs | – | ✓ | ✓ (HR team) | – |
| `hr.payroll.write`           | Draft payroll run (preparer) | – | – | ✓ (preparer) | – |
| `hr.payroll.admin`           | Approve payroll run (four-eyes: preparer ≠ approver) | – | ✓ | – | – |
| `hr.shifts.read`             | View shift schedules | – | ✓ | ✓ | – |
| `hr.shifts.admin`            | Build / publish shift schedules | – | ✓ | ✓ (scheduler) | – |
| `hr.personnel-docs.read`     | View personnel document vault | – | ✓ | – | – |
| `hr.personnel-docs.admin`    | Manage personnel document vault | – | ✓ | – | – |
| `hr.self-service.read`       | Employee self-service view | – | – | – | ✓ (employee) |
| `hr.self-service.write`      | Submit self-service requests (leave, doc) | – | – | – | ✓ (employee) |

`hr.self-service.*` is auto-granted to any user with an Employee record; other HR permissions are explicit grants.

---

## Education (`education.*`) — Tenant scope (organization-filtered, Tier 3b)

Added 2026-04-22 by Prompt 3 Education Agent.

| Permission | Description | PA | TA | TU | PU |
|------------|-------------|:--:|:--:|:--:|:--:|
| `education.students.read`        | Read student records | – | ✓ | ✓ (teacher/registrar) | – |
| `education.students.write`       | Create / update student records | – | ✓ | ✓ (registrar) | – |
| `education.students.admin`       | Withdraw / merge / export student records | – | ✓ | – | – |
| `education.guardians.read`       | Read guardian records | – | ✓ | ✓ | – |
| `education.guardians.write`      | Create / update guardian records | – | ✓ | ✓ | – |
| `education.enrollments.read`     | Read enrollments | – | ✓ | ✓ (registrar) | – |
| `education.enrollments.write`    | Create / update enrollments | – | ✓ | ✓ (registrar) | – |
| `education.enrollments.admin`    | Close / re-open / bulk-transfer enrollments | – | ✓ | – | – |
| `education.applications.read`    | Read admissions applications | – | ✓ | ✓ (admissions) | – |
| `education.applications.write`   | Create / update applications | – | ✓ | ✓ (admissions) | – |
| `education.applications.admin`   | Record admissions decision | – | ✓ | ✓ (admissions) | – |
| `education.attendance.read`      | Read student attendance | – | ✓ | ✓ (teacher) | – |
| `education.attendance.write`     | Record student attendance | – | ✓ | ✓ (teacher) | – |
| `education.grades.read`          | Read gradebook | – | ✓ | ✓ (teacher) | – |
| `education.grades.write`         | Post grades | – | ✓ | ✓ (teacher) | – |
| `education.grades.admin`         | Publish / unpublish gradebook | – | ✓ | – | – |
| `education.accreditation.read`   | Read accreditation records | – | ✓ | ✓ (ops) | – |
| `education.accreditation.admin`  | Create / renew / revoke accreditation records | – | ✓ | ✓ (ops) | – |
| `education.appointments.read`    | Read tours, parent-teacher meetings | – | ✓ | ✓ (staff) | – |
| `education.appointments.write`   | Schedule tours, parent-teacher meetings | – | ✓ | ✓ (staff) | – |
| `education.portal.read`          | Guardian portal scoped view | – | – | – | ✓ (guardian) |

`education.portal.read` is always scoped via `GuardianLink.student_id` — backend filters responses to the calling guardian's linked students.
