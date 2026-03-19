# Nexora - Enterprise Modular Platform

## Documentation Index

### Architecture
| Document | Description |
|----------|-------------|
| [Project Vision](./PROJECT_VISION.md) | Product vision, target market, value proposition |
| [Architecture Overview](./architecture/OVERVIEW.md) | System architecture, tech stack, infrastructure |
| [Module System](./architecture/MODULE_SYSTEM.md) | Plugin architecture, install/uninstall, cross-module communication |
| [Auth & Identity](./auth/IDENTITY.md) | Authentication, authorization, multi-tenancy |
| [Module Dependencies](./diagrams/module-dependencies.md) | Module dependency graph |

### Standards
| Document | Description |
|----------|-------------|
| [Coding Standards](./standards/CODING_STANDARDS.md) | C# conventions, CQRS, API, testing, module plugin rules |
| [Localization Standards](./standards/LOCALIZATION_STANDARDS.md) | i18n rules, lockey_ format, BE + FE localization |
| [Documentation Standards](./standards/DOCUMENTATION_STANDARDS.md) | Mermaid diagrams, ADR template, module spec template |
| [Release Standards](./standards/RELEASE_STANDARDS.md) | Versioning, CI/CD, Docker, migrations |

### Architecture Decisions
| Document | Description |
|----------|-------------|
| [ADR-001](./decisions/ADR-001-modular-monolith.md) | Modular Monolith architecture |
| [ADR-002](./decisions/ADR-002-multi-tenancy.md) | Schema-per-tenant multi-tenancy |

### Module Specifications
| Module | Spec | Phase |
|--------|------|-------|
| Identity & Access | [SPEC](./modules/identity/SPEC.md) | Core |
| Contact Management | [SPEC](./modules/contacts/SPEC.md) | Core |
| Notification Engine | [SPEC](./modules/notifications/SPEC.md) | Core |
| Document Management | [SPEC](./modules/documents/SPEC.md) | Core |
| CRM | [SPEC](./modules/crm/SPEC.md) | Phase 2 |
| Donations & Fundraising | [SPEC](./modules/donations/SPEC.md) | Phase 2 |
| Sponsorship | [SPEC](./modules/sponsorship/SPEC.md) | Phase 2 |
| Event Management | [SPEC](./modules/events/SPEC.md) | Phase 2 |
| Education Management | [SPEC](./modules/education/SPEC.md) | Phase 3 |
| Subscription & Billing | [SPEC](./modules/subscription/SPEC.md) | Phase 3 |
| Website & CMS | [SPEC](./modules/cms/SPEC.md) | Phase 3 |
| Surveys & Feedback | [SPEC](./modules/surveys/SPEC.md) | Phase 3 |
| Accounting & Finance | [SPEC](./modules/accounting/SPEC.md) | Phase 4 |
| HR & Payroll | [SPEC](./modules/hr/SPEC.md) | Phase 4 |
| Point of Sale | [SPEC](./modules/pos/SPEC.md) | Phase 4 |
| Fleet Management | [SPEC](./modules/fleet/SPEC.md) | Phase 4 |
| Inventory & Assets | [SPEC](./modules/inventory/SPEC.md) | Phase 4 |
| Project Management | [SPEC](./modules/projects/SPEC.md) | Phase 4 |

### Roadmap
| Document | Description |
|----------|-------------|
| [Product Roadmap](./roadmap/ROADMAP.md) | Phases, Gantt chart, deliverables |
