# Module Specifications

This directory hosts the per-module technical specs, organized by **tier** as defined in [ADR-016 — Module Tier Classification](../decisions/0016-module-tier-classification.md).

## Layout

```
modules/
├── tier-1-core/          # Platform Core — required in every deployment
├── tier-2-enterprise/    # Enterprise Edition — B2B commercial modules
├── tier-3a-ngo/          # NGO Edition — non-profit / fundraising modules
└── tier-3b-education/    # Education Edition — schools / academic modules
```

Tier-4 (extensions / deferred) specs live in [`../_archive/specs-legacy/`](../_archive/specs-legacy/README.md).

## Tier 1 — Platform Core

Modules every tenant gets. See ADR-016 §Tier 1.

| Module | Spec |
|--------|------|
| Identity & Access Management | [`tier-1-core/identity/SPEC.md`](tier-1-core/identity/SPEC.md) |
| Contacts | [`tier-1-core/contacts/SPEC.md`](tier-1-core/contacts/SPEC.md) |
| Notifications | [`tier-1-core/notifications/SPEC.md`](tier-1-core/notifications/SPEC.md) |
| Documents | [`tier-1-core/documents/SPEC.md`](tier-1-core/documents/SPEC.md) |
| Audit | [`tier-1-core/audit/SPEC.md`](tier-1-core/audit/SPEC.md) |
| Reporting | [`tier-1-core/reporting/SPEC.md`](tier-1-core/reporting/SPEC.md) |
| Portal Framework | [`tier-1-core/portal-framework/SPEC.md`](tier-1-core/portal-framework/SPEC.md) *(stub — full spec TODO)* |
| Admin Dashboard | [`tier-1-core/admin-dashboard/SPEC.md`](tier-1-core/admin-dashboard/SPEC.md) *(stub — full spec TODO)* |

## Tier 2 — Enterprise Edition

B2B commercial modules. See ADR-016 §Tier 2.

| Module | Spec |
|--------|------|
| CRM | [`tier-2-enterprise/crm/SPEC.md`](tier-2-enterprise/crm/SPEC.md) |
| Finance *(renamed from accounting)* | [`tier-2-enterprise/finance/SPEC.md`](tier-2-enterprise/finance/SPEC.md) |
| Projects | [`tier-2-enterprise/projects/SPEC.md`](tier-2-enterprise/projects/SPEC.md) |
| HR & Payroll | [`tier-2-enterprise/hr/SPEC.md`](tier-2-enterprise/hr/SPEC.md) |
| Subscription & Billing | [`tier-2-enterprise/subscription/SPEC.md`](tier-2-enterprise/subscription/SPEC.md) |

## Tier 3a — NGO Edition

Non-profit / fundraising modules. See ADR-016 §Tier 3a.

| Module | Spec |
|--------|------|
| Fundraising *(merged: donations + sponsorship)* | [`tier-3a-ngo/fundraising/SPEC.md`](tier-3a-ngo/fundraising/SPEC.md) |
| Events (NGO) | [`tier-3a-ngo/events-ngo/SPEC.md`](tier-3a-ngo/events-ngo/SPEC.md) |

## Tier 3b — Education Edition

Schools / academic modules. See ADR-016 §Tier 3b.

| Module | Spec |
|--------|------|
| Education | [`tier-3b-education/education/SPEC.md`](tier-3b-education/education/SPEC.md) |

## Tier 4 — Archived / Deferred

POS, Fleet, Inventory, Surveys, and CMS have been archived to [`../_archive/specs-legacy/`](../_archive/specs-legacy/README.md) pending Phase 4 re-evaluation.
