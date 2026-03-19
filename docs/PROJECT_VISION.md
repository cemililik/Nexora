# Nexora - Project Vision

## Product Name
**Nexora** — Next-Generation Enterprise Operations Platform

## Tagline
> "Unify. Automate. Scale."

## 1. Problem Statement

Non-profit organizations, educational institutions, and multi-entity enterprises struggle with:

- **Fragmented systems**: CRM in one tool, accounting in another, donations in a third
- **No unified contact view**: A person can be a donor, parent, volunteer, and customer simultaneously — but exists as separate records across systems
- **Operational blindness**: Management cannot see consolidated reports across entities
- **Vendor lock-in**: Existing ERP solutions (Odoo, SAP, Dynamics) are either too expensive, too rigid, or require heavy customization
- **Poor digital presence**: Website, donation pages, and enrollment forms are disconnected from backend operations

## 2. Solution

Nexora is a **modular, multi-tenant enterprise platform** that provides:

- **Pluggable module architecture**: Organizations install only the modules they need
- **Multi-organization support**: Single deployment serves multiple entities with data isolation
- **360-degree contact view**: One contact record across all modules (CRM, donations, enrollment, POS)
- **Modern web portals**: Each organization gets its own branded portal
- **Real-time operations**: Event-driven architecture for instant updates across modules

## 3. Target Market

| Segment | Example Organizations |
|---------|----------------------|
| Non-Profit / Foundations | Charity organizations, religious foundations, NGOs |
| Educational Institutions | K-12 schools, academies, boarding schools, summer camps |
| Multi-Entity Enterprises | Holding companies, franchise networks |
| Social Service Organizations | Community centers, food banks, volunteer networks |

## 4. Core Value Propositions

1. **Modular by Design**: Pay for and deploy only what you need. Start with CRM, add Donations later.
2. **Multi-Tenant Native**: Built from day one for multi-organization isolation with consolidated reporting.
3. **Modern Stack**: Cloud-native, API-first, event-driven. No legacy baggage.
4. **Self-Service Portals**: Donors, parents, volunteers get their own dashboards — no admin intervention.
5. **Extensible**: Plugin architecture allows custom modules without touching core.
6. **White-Label Ready**: Each tenant can have its own branding, domain, and theme.

## 5. Module Map

### Core Platform (Always Installed)
- **Identity & Access Management** — Multi-tenant auth, RBAC, organization management
- **Contact Management** — Unified contact registry shared across all modules
- **Notification Engine** — Email, SMS, WhatsApp, Push notifications
- **Document Management** — File storage, digital signatures, archival
- **Reporting Engine** — Cross-module dashboards and report builder
- **Portal Framework** — Self-service portal infrastructure for external users

### Business Modules (Installable)
| Module | Key Capabilities |
|--------|-----------------|
| **CRM** | Leads, pipelines, activities, segmentation, marketing campaigns |
| **Donations & Fundraising** | Online donations, campaigns, recurring giving, receipts, fund tracking |
| **Sponsorship** | Sponsor-beneficiary matching, installments, progress tracking |
| **Education Management** | Enrollment pipeline, student records, academic calendar, parent portal |
| **Subscription & Billing** | Recurring invoices, payment plans, tuition management |
| **Accounting & Finance** | Journals, chart of accounts, expense management, bank reconciliation |
| **Project Management** | Projects, tasks, Kanban boards, construction cost tracking |
| **HR & Payroll** | Employee records, contracts, leave, payroll |
| **Event Management** | Seasonal events, campaigns (Ramadan, fundraisers), registrations |
| **Point of Sale (POS)** | Tablet/phone-friendly sales terminal, inventory integration |
| **Fleet Management** | Vehicles, insurance tracking, fuel logs, assignments |
| **Inventory & Assets** | Warehouses, stock movements, fixed asset tracking |
| **Survey & Feedback** | Form builder, satisfaction surveys, analytics |
| **Website & CMS** | Multi-site builder, blog, landing pages, SEO |

## 6. Competitive Landscape

| Competitor | Weakness Nexora Addresses |
|-----------|---------------------------|
| Odoo | Heavy customization needed, Python monolith, upgrade pain |
| Salesforce NPSP | Expensive, complex for mid-size orgs, US-centric |
| Microsoft Dynamics | Enterprise pricing, steep learning curve |
| Custom Solutions | No modularity, single-purpose, maintenance burden |
| Bloomerang / Donorbox | Donation-only, no ERP capabilities |

## 7. Deployment Models

| Model | Description | Target | Setup Time |
|-------|-------------|--------|------------|
| **Nexora Cloud (SaaS)** | Fully managed, shared infrastructure, schema-per-tenant isolation. We handle backups, updates, scaling. | SMBs, NGOs, startups | Minutes |
| **Nexora Dedicated** | Single-tenant managed deployment in customer's preferred region. We manage ops, customer controls data location. | Enterprise, regulated orgs | 1-2 days |
| **Nexora Self-Hosted** | Customer installs via Helm chart on their own Kubernetes cluster. Full data sovereignty, air-gapped support. | Government, military, healthcare | ~1 week |

All models use the **same codebase, same Helm chart, same container images**. Module licensing controls feature activation. Pricing tiers: Starter (25 users, 2 modules), Professional (100 users, 6 modules), Enterprise (unlimited).

> **Details**: [ADR-003: Deployment Strategy](./decisions/ADR-003-deployment-strategy.md) | [Tenant Operations](./operations/TENANT_OPERATIONS.md) | [Helm Installation Guide](./operations/HELM_INSTALLATION.md)

## 8. Success Metrics

- Module adoption rate per tenant
- Time to onboard a new organization (target: < 1 day for basic setup)
- Contact deduplication rate (360-degree view effectiveness)
- Donor retention rate improvement (for NGO tenants)
- Student enrollment conversion rate (for education tenants)
