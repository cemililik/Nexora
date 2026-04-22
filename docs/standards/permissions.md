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
  - **Tenant User** — baseline read + self-service write (`*.view` on installed modules plus own-entity `update` where applicable). No admin, no destructive actions.
  - **Portal User** — customer/member-facing role for the public portal; restricted to `portal.*` permissions and read-only access to their own records (e.g., `contacts.contact.view` gated by `ContactId == self`).
- **Default grants:** every module spec's Permission Matrix (§5) MUST list which of the four built-in roles receives each permission on install. The Identity module's seeder reads that matrix during `ModuleInstalled` processing.

## 1. Permission Naming

```
{module}.{resource}.{action}
```

| Part | Rule | Examples |
|------|------|----------|
| `{module}` | Module name, lowercase, single token | `identity`, `contacts`, `crm`, `donations` |
| `{resource}` | Noun, singular or plural matching DB table convention | `user`, `role`, `lead`, `donation`, `campaign` |
| `{action}` | Verb from a closed set | `view`, `create`, `update`, `delete`, `export`, `import`, `approve`, `assign`, `manage` |

Examples:

```
identity.user.view
identity.user.create
identity.role.manage
contacts.contact.export
crm.lead.assign
crm.pipeline.manage
donations.donation.refund
donations.campaign.view
```

### Rules

- Single dot between each part. Exactly 3 parts.
- Lowercase. Hyphen allowed inside a part only when the table name itself is hyphenated
  (rare). Prefer a single-word resource name.
- `manage` is a superset — it implies `view|create|update|delete` for that resource; prefer
  fine-grained permissions over `manage` unless the resource is inherently atomic.
- Custom actions on sub-resources follow the same shape:
  `crm.lead.convert`, `donations.donation.refund`.

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
        registry.Register("crm.lead.view",     scope: PermissionScope.Tenant);
        registry.Register("crm.lead.create",   scope: PermissionScope.Tenant);
        registry.Register("crm.lead.update",   scope: PermissionScope.Tenant);
        registry.Register("crm.lead.delete",   scope: PermissionScope.Tenant);
        registry.Register("crm.lead.assign",   scope: PermissionScope.Tenant);
        registry.Register("crm.lead.convert",  scope: PermissionScope.Tenant);
        registry.Register("crm.pipeline.view", scope: PermissionScope.Tenant);
        registry.Register("crm.pipeline.manage", scope: PermissionScope.Tenant);
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
[Authorize(Policy = "crm.lead.create")]
public static async Task<IResult> CreateLead(...) { ... }
```

Every HTTP endpoint MUST have either:

- `[Authorize(Policy = "{module}.{resource}.{action}")]`, or
- `[AllowAnonymous]` **with a one-line justification comment** immediately above.

### 4.2 Backend — Command / Query

For operations invoked from background jobs or integration events, use
`IAuthorizationService.AuthorizeAsync(user, "crm.lead.assign", ct)` at the handler entry.
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
if (hasPermission('crm.lead.create')) { /* show button */ }
```

Route guard pattern is defined in `shared/hooks/usePermissions.ts` — do not reimplement.

## 5. Permission Matrix Template

Every module spec (`docs/modules/{module}/SPEC.md` once migrated to
`docs/modules/tier-*/...`) MUST include a **Permission Matrix**. Fill this table when
writing the spec:

```markdown
## Permission Matrix

Scope: Tenant  (or: Platform — for NMP modules)

| Resource \ Action | view | create | update | delete | export | import | approve | assign | manage | custom (list) |
|-------------------|:----:|:------:|:------:|:------:|:------:|:------:|:-------:|:------:|:------:|---------------|
| <resource-1>      |  ✓   |   ✓    |   ✓    |   ✓    |   ✓    |   –    |    –    |   –    |   ✓    | convert       |
| <resource-2>      |  ✓   |   ✓    |   ✓    |   –    |   –    |   –    |    –    |   –    |   ✓    | –             |

Legend: ✓ = permission exists, – = not applicable for this resource.
```

Rules for authors:

- Every row and every `✓` corresponds to a permission registered at module startup
  (§3) — if it's in the matrix, the code MUST declare it.
- Custom actions list is freeform but each entry must be a registered permission.
- Role presets (suggested role → permission bundle) go in a separate table below the matrix.

### Example — Identity Module (partial)

```markdown
| Resource \ Action | view | create | update | delete | export | import | approve | assign | manage | custom |
|-------------------|:----:|:------:|:------:|:------:|:------:|:------:|:-------:|:------:|:------:|--------|
| user              |  ✓   |   ✓    |   ✓    |   ✓    |   ✓    |   –    |    –    |   –    |   ✓    | –      |
| role              |  ✓   |   ✓    |   ✓    |   ✓    |   –    |   –    |    –    |   ✓    |   ✓    | –      |
| organization      |  ✓   |   ✓    |   ✓    |   ✓    |   –    |   –    |    –    |   –    |   ✓    | –      |
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

### 8.1 Identity (`identity.*`) — Tenant scope

| Resource | view | create | update | delete | manage | Default grants |
|----------|:----:|:------:|:------:|:------:|:------:|----------------|
| `user`   |  ✓   |   ✓    |   ✓    |   ✓    |   ✓    | TA: all; TU: view |
| `role`   |  ✓   |   ✓    |   ✓    |   ✓    |   ✓    | TA: all |
| `organization` |  ✓ | ✓ | ✓ | ✓ | ✓ | TA: all; TU: view |
| `invitation` | ✓ | ✓ | – | ✓ | – | TA: all |
| `session` | ✓ | – | – | ✓ | – | TA: all; TU: view+delete own |

Custom actions: `identity.user.impersonate` (TA only, audited), `identity.users.link_contact` (TA only — links/unlinks a user to a Contacts record; system accounts are always rejected, unlink also triggered automatically on `ContactGdprDeletedIntegrationEvent`).

### 8.2 Contacts (`contacts.*`) — Tenant scope

> Permissions follow platform convention `{module}.{resource}.{read|write|delete|admin}` — `write` covers both create and update. `admin` covers merge and GDPR anonymize.

| Resource | read | write | delete | admin | Default grants |
|----------|:----:|:-----:|:------:|:-----:|----------------|
| `contact` | ✓ | ✓ | ✓ | ✓ | TA: all; TU: read+write; PU: read own |
| `tag`     | ✓ | ✓ | ✓ | – | TA: all; TU: read |
| `consent` | ✓ | ✓ | – | – | TA: all; TU: read+write own |

### 8.3 Notifications (`notifications.*`) — Tenant scope

| Resource | view | create | update | delete | send | manage | Default grants |
|----------|:----:|:------:|:------:|:------:|:----:|:------:|----------------|
| `notification` | ✓ | – | – | – | ✓ | – | TA: all; TU: view |
| `template` | ✓ | ✓ | ✓ | ✓ | – | ✓ | TA: all |
| `provider` | ✓ | ✓ | ✓ | ✓ | – | ✓ | TA: all |

### 8.4 Documents (`documents.*`) — Tenant scope

| Resource | view | create | update | delete | download | share | manage | Default grants |
|----------|:----:|:------:|:------:|:------:|:--------:|:-----:|:------:|----------------|
| `document` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | TA: all; TU: view+create+download |
| `folder`   | ✓ | ✓ | ✓ | ✓ | – | – | ✓ | TA: all; TU: view |

### 8.5 Audit (`audit.*`) — Tenant & Platform scopes

| Resource | view | export | purge | Default grants |
|----------|:----:|:------:|:-----:|----------------|
| `event`  | ✓ | ✓ | ✓ | TA: view+export; PA: view+export+purge (platform scope) |

### 8.6 Reporting (`reporting.*`) — Tenant scope

| Resource | view | create | update | delete | run | schedule | manage | Default grants |
|----------|:----:|:------:|:------:|:------:|:---:|:--------:|:------:|----------------|
| `report` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | TA: all; TU: view+run |
| `dashboard` | ✓ | ✓ | ✓ | ✓ | – | – | ✓ | TA: all; TU: view |

### 8.7 Portal Framework (`portal.*`) — Tenant scope

| Resource | view | update | manage | Default grants |
|----------|:----:|:------:|:------:|----------------|
| `profile`  | ✓ | ✓ | – | PU: view+update own |
| `layout`   | ✓ | ✓ | ✓ | TA: all |
| `content-block` | ✓ | ✓ | ✓ | TA: all |

### 8.8 Admin Dashboard (`admin.*`) — Tenant scope

| Resource | view | update | manage | Default grants |
|----------|:----:|:------:|:------:|----------------|
| `widget`   | ✓ | ✓ | ✓ | TA: all; TU: view |
| `layout`   | ✓ | ✓ | ✓ | TA: all |
| `setting`  | ✓ | ✓ | ✓ | TA: all |

> Tier 2 / Tier 3 matrices are appended by their respective module owners. This document remains the canonical index.

## CRM

Scope: Tenant. Module: `crm` (Tier 2 — Enterprise).

Permissions declared at module startup (`CrmModule.OnStartupAsync`):

| Permission | Purpose |
|------------|---------|
| `crm.leads.read` | View leads, lead lists, Kanban. |
| `crm.leads.write` | Create, update, assign, qualify, disqualify, move-stage, archive leads. |
| `crm.opportunities.read` | View opportunities and pipeline values. |
| `crm.opportunities.write` | Create / update / move-stage / win / lose opportunities. |
| `crm.opportunities.manage` | Reopen closed opportunities; override assigned owner. |
| `crm.pipelines.read` | View pipeline / stage / custom-field definitions. |
| `crm.pipelines.manage` | Create, rename, archive pipelines; add / reorder / remove stages; manage per-pipeline custom field defs; configure stage-transition automations. |
| `crm.activities.read` | View activities on leads / opportunities / contacts. |
| `crm.activities.write` | Log, edit, complete, reschedule activities. |
| `crm.campaigns.read` | View campaign list, analytics. |
| `crm.campaigns.manage` | Create, edit, schedule, cancel marketing campaigns. |

### Permission Matrix

| Resource \ Action | read | write | manage |
|-------------------|:----:|:-----:|:------:|
| leads | ✓ | ✓ | – |
| opportunities | ✓ | ✓ | ✓ |
| pipelines | ✓ | – | ✓ |
| activities | ✓ | ✓ | – |
| campaigns | ✓ | – | ✓ |

Role presets (suggested):

- **CRM Viewer** — all `*.read`.
- **SDR** — `crm.leads.*`, `crm.activities.*`, `crm.opportunities.read`, `crm.pipelines.read`, `crm.campaigns.read`.
- **Account Executive** — SDR + `crm.opportunities.write`.
- **RevOps Admin** — all `crm.*` including `crm.pipelines.manage`, `crm.opportunities.manage`, `crm.campaigns.manage`.

## Finance

Scope: Tenant. Module: `finance` (Tier 2 — Enterprise Core).

Permissions declared at module startup (`FinanceModule.OnStartupAsync`):

| Permission | Purpose |
|------------|---------|
| `finance.accounts.read` | View chart of accounts and account balances. |
| `finance.accounts.manage` | Create, update, and deactivate GL accounts and COA structure. |
| `finance.journal.read` | View journal entries and lines. |
| `finance.journal.post` | Create, approve, post, and void journal entries. |
| `finance.budgets.read` | View budgets and variance reports. |
| `finance.budgets.manage` | Create, edit, activate, and close budgets and budget lines. |
| `finance.bank.read` | View bank accounts and imported bank transactions. |
| `finance.bank.reconcile` | Import statements, match transactions, and approve reconciliations. |
| `finance.reports.read` | Run Finance-sourced reports (trial balance, income statement, balance sheet, cash flow) via the Reporting module. |

### Permission Matrix

| Resource \ Action | read | manage | post | reconcile |
|-------------------|:----:|:------:|:----:|:---------:|
| accounts | ✓ | ✓ | – | – |
| journal | ✓ | – | ✓ | – |
| budgets | ✓ | ✓ | – | – |
| bank | ✓ | – | – | ✓ |
| reports | ✓ | – | – | – |

Role presets (suggested):

- **Finance Viewer** — all `finance.*.read`.
- **Accountant** — Viewer + `finance.journal.post`, `finance.bank.reconcile`.
- **Controller** — Accountant + `finance.accounts.manage`, `finance.budgets.manage`.

## Subscription

Scope: Tenant. Module: `subscription` (Tier 2 — Enterprise Core). Declared by the Tier-2
Subscription module; see `docs/modules/tier-2-enterprise/subscription/SPEC.md` §12.

Permissions declared at module startup (`SubscriptionModule.OnStartupAsync`):

| Permission | Purpose |
|------------|---------|
| `subscription.plans.read` | View plan catalog. |
| `subscription.plans.manage` | Create, update, activate / deactivate plans. |
| `subscription.subscriptions.read` | View subscriptions across all contacts. |
| `subscription.subscriptions.write` | Create, upgrade, downgrade, cancel subscriptions. |
| `subscription.subscriptions.admin` | Administrative overrides (backdate, waive proration, force-cancel past `unpaid`). |
| `subscription.invoices.read` | View invoices. |
| `subscription.payment-methods.read` | View payment methods attached to contacts. |
| `subscription.payment-methods.write` | Register, replace, remove payment methods; set default. |

### Permission Matrix

| Resource \ Action | read | write | admin | manage | custom |
|-------------------|:----:|:-----:|:-----:|:------:|--------|
| plans             |  ✓   |   –   |   –   |   ✓    | activate, deactivate |
| subscriptions     |  ✓   |   ✓   |   ✓   |   –    | upgrade, downgrade, cancel |
| invoices          |  ✓   |   –   |   –   |   –    | – |
| payment-methods   |  ✓   |   ✓   |   –   |   –    | set-default |

Portal self-service endpoints (`/portal/**`) do not require a tenant-wide permission — the
handler scopes queries and mutations to the caller's own `contact_id`.

Role presets (suggested):

- **Billing Viewer** — all `subscription.*.read`.
- **Billing Operator** — Viewer + `subscription.subscriptions.write`, `subscription.payment-methods.write`.
- **Billing Admin** — Operator + `subscription.plans.manage`, `subscription.subscriptions.admin`.

## 9. Fundraising

Scope: Tenant. Module ID: `fundraising`. Spec:
[docs/modules/tier-3a-ngo/fundraising/SPEC.md](../modules/tier-3a-ngo/fundraising/SPEC.md).

Registered permissions:

```
fundraising.donations.read
fundraising.donations.write
fundraising.donations.refund
fundraising.donations.export
fundraising.donations.import
fundraising.campaigns.read
fundraising.campaigns.write
fundraising.campaigns.manage
fundraising.recurring.read
fundraising.recurring.write
fundraising.recurring.manage
fundraising.sponsorships.read
fundraising.sponsorships.write
fundraising.sponsorships.manage
fundraising.sponsorships.progress.write
fundraising.beneficiaries.read
fundraising.beneficiaries.write
fundraising.beneficiaries.manage
fundraising.receipts.read
fundraising.receipts.regenerate
fundraising.qurban.read
fundraising.qurban.write
```

Matrix:

| Resource \ Action | read | write | manage | custom |
|-------------------|:----:|:-----:|:------:|--------|
| donations         |  ✓   |   ✓   |   ✓    | refund, export, import |
| campaigns         |  ✓   |   ✓   |   ✓    | – |
| recurring         |  ✓   |   ✓   |   ✓    | pause, resume, cancel (via `write`) |
| sponsorships      |  ✓   |   ✓   |   ✓    | progress.write |
| beneficiaries     |  ✓   |   ✓   |   ✓    | – |
| receipts          |  ✓   |   –   |   –    | regenerate |
| qurban *(cap-4)*  |  ✓   |   ✓   |   –    | – |

Notes:

- `fundraising.donations.refund` is audited MUST (security-event; see
  audit-coverage.md §Fundraising).
- `fundraising.qurban.*` is gated by the tenant feature flag
  `fundraising.islamic_extensions.enabled` — permissions are always registered, but
  endpoints return `404` when the flag is off.
- Donor portal self-service on own recurring plans / sponsorships is authorised by
  ownership (`plan.DonorContactId == currentUser.ContactId`) **or** the corresponding
  `.write` permission; the handler checks in that order.

Role presets (suggested):

- **Fundraising Viewer** — all `fundraising.*.read`.
- **Fundraising Operator** — Viewer + `fundraising.donations.write`,
  `fundraising.campaigns.write`, `fundraising.sponsorships.write`,
  `fundraising.sponsorships.progress.write`, `fundraising.beneficiaries.write`.
- **Fundraising Admin** — Operator + `fundraising.donations.refund`,
  `fundraising.receipts.regenerate`, all `.manage` permissions.

---

## Projects (`projects.*`) — Tenant scope (organization-filtered)

Added 2026-04-22 by Prompt 3 Projects Agent.

| Permission | Description | Platform Admin | Tenant Admin | Tenant User | Portal User |
|------------|-------------|:--:|:--:|:--:|:--:|
| `projects.projects.read`      | Read project list / detail | – | ✓ | ✓ | ✓ (own) |
| `projects.projects.write`     | Create / update project | – | ✓ | ✓ | – |
| `projects.projects.close`     | Close / reopen project | – | ✓ | – | – |
| `projects.tasks.read/write`   | Task CRUD (board, detail) | – | ✓ | ✓ | ✓ (own) |
| `projects.time.read/write`    | Log / list time entries | – | ✓ | ✓ (own) | – |
| `projects.budgets.read`       | View project budget | – | ✓ | ✓ | – |
| `projects.budgets.manage`     | Create / approve / adjust budget | – | ✓ | – | – |
| `projects.cost-entries.read/write` | Log / view cost entries | – | ✓ | ✓ | – |
| `projects.members.manage`     | Add / remove project members | – | ✓ | – | – |
| `projects.subcontracts.read`  | View subcontracts | – | ✓ | ✓ | – |
| `projects.subcontracts.manage`| Create / activate / terminate subcontracts | – | ✓ | – | – |

Portal stakeholder routes are gated by a scoped role that intersects `projects.projects.read` + `projects.tasks.read` with the calling contact's linked project list.

---

## HR (`hr.*`) — Tenant scope (organization-filtered)

Added 2026-04-22 by Prompt 3 HR Agent.

| Permission | Description | PA | TA | TU | PU |
|------------|-------------|:--:|:--:|:--:|:--:|
| `hr.employees.read`    | Read employee records | – | ✓ | ✓ (HR team) | – |
| `hr.employees.write`   | Create / update employee records | – | ✓ | – | – |
| `hr.employees.admin`   | Terminate, rewrite manager, export | – | ✓ | – | – |
| `hr.contracts.read/manage` | Read / manage employment contracts | – | ✓ | – | – |
| `hr.leaves.read/manage/approve` | Leave CRUD and approval | – | ✓ | ✓ (approver) | – |
| `hr.payroll.read`      | Read payroll runs | – | ✓ | ✓ (HR team) | – |
| `hr.payroll.prepare`   | Draft payroll run | – | – | ✓ (preparer) | – |
| `hr.payroll.approve`   | Approve payroll run (four-eyes: preparer ≠ approver) | – | ✓ | – | – |
| `hr.shifts.read/manage`| Shift scheduling | – | ✓ | ✓ (scheduler) | – |
| `hr.personnel-docs.read/manage` | Access personnel document vault | – | ✓ | – | – |
| `hr.self-service.read/submit` | Employee self-service view + leave submit | – | – | – | ✓ (employee) |

`hr.self-service.*` is auto-granted to any user with an Employee record; other HR permissions are explicit grants.

---

## Education (`education.*`) — Tenant scope (organization-filtered, Tier 3b)

Added 2026-04-22 by Prompt 3 Education Agent.

| Permission | Description | PA | TA | TU | PU |
|------------|-------------|:--:|:--:|:--:|:--:|
| `education.students.read/write/admin` | Student record CRUD + admin | – | ✓ | ✓ (teacher/registrar) | – |
| `education.guardians.read/write`       | Guardian records | – | ✓ | ✓ | – |
| `education.enrollments.read/manage`    | Enrollment lifecycle | – | ✓ | ✓ (registrar) | – |
| `education.applications.read/manage/decision` | Admissions pipeline ops + decision | – | ✓ | ✓ (admissions) | – |
| `education.attendance.read/record`     | Record / read student attendance | – | ✓ | ✓ (teacher) | – |
| `education.grades.read/post/publish`   | Gradebook operations | – | ✓ | ✓ (teacher) | – |
| `education.accreditation.read/manage`  | Accreditation records | – | ✓ | ✓ (ops) | – |
| `education.appointments.read/schedule` | Tours, parent-teacher meetings | – | ✓ | ✓ (staff) | – |
| `education.portal.self-view`           | Guardian portal scoped view | – | – | – | ✓ (guardian) |

`education.portal.self-view` is always scoped via `GuardianLink.student_id` — backend filters responses to the calling guardian's linked students.
