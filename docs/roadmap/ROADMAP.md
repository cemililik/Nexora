# Nexora - Product Roadmap

## Phase Overview

```mermaid
gantt
    title Nexora Product Roadmap
    dateFormat YYYY-MM-DD
    axisFormat %b %Y

    section Phase 0 - Foundation
    Repo & CI/CD setup           :p0a, 2026-04-01, 2w
    Docker Compose (all infra)   :p0b, after p0a, 2w
    Multi-tenant infrastructure  :p0c, after p0b, 3w
    Keycloak & APISIX setup      :p0d, after p0b, 2w
    SharedKernel & Module loader :p0e, after p0c, 2w
    Observability stack          :p0f, after p0d, 1w

    section Phase 1 - Core Platform
    Identity module              :p1a, after p0e, 4w
    Contact module               :p1b, after p1a, 3w
    Notification Engine          :p1c, after p1b, 3w
    Document Management          :p1d, after p1b, 3w
    Reporting Engine             :p1e, after p1c, 2w
    Portal Framework (Next.js)   :p1f, after p1d, 2w

    section Phase 2 - NGO/Foundation
    CRM module                   :p2a, after p1b, 4w
    Donations module             :p2b, after p2a, 5w
    Sponsorship module           :p2c, after p2b, 4w
    Event Management             :p2d, after p2a, 3w
    Kumbara & Kumanya            :p2e, after p2b, 2w

    section Phase 3 - Education & CMS
    Education module             :p3a, after p2a, 4w
    Subscription & Billing       :p3b, after p3a, 3w
    Website & CMS                :p3c, after p1f, 4w
    Surveys & Feedback           :p3d, after p3a, 2w

    section Phase 4 - Operations
    Accounting & Finance         :p4a, after p2b, 5w
    HR & Payroll                 :p4b, after p4a, 3w
    Point of Sale                :p4c, after p4a, 3w
    Fleet Management             :p4d, after p4b, 2w
    Inventory & Assets           :p4e, after p4c, 2w
    Project Management           :p4f, after p4a, 3w
```

### Module Dependency Diagram
See [Module Dependencies](../diagrams/module-dependencies.md) for the full dependency graph.

---

## Phase 0: Foundation & Infrastructure
> **Goal**: Development environment, CI/CD, core architecture, database design

### Deliverables
- [x] Repository structure (solution, projects, folder conventions)
- [x] Development environment setup (Docker Compose for all infra)
- [ ] CI/CD pipeline (GitHub Actions: build, test, lint, security scan)
- [x] PostgreSQL multi-tenant infrastructure (schema management, migrations)
- [ ] Keycloak setup (realm configuration, tenant-aware authentication)
- [ ] APISIX gateway configuration (routing, rate limiting, JWT validation)
- [x] Dapr sidecar setup (pub/sub, state store, secret store bindings)
- [x] Redis configuration (caching layer, session management)
- [x] Kafka topic design and cluster setup
- [ ] HashiCorp Vault integration (secret management)
- [x] MinIO setup (object storage, bucket-per-tenant)
- [ ] Observability stack (OpenTelemetry, Grafana, Loki, Tempo)
- [x] Shared kernel library (common types, base entities, multi-tenant middleware)
- [x] Module loader & plugin architecture
- [x] API documentation infrastructure (OpenAPI/Swagger)
- [x] Coding standards enforcement (EditorConfig, analyzers, pre-commit hooks)

### Technical Milestones
1. [x] `docker compose up` brings entire stack online
2. [ ] A request flows: APISIX → .NET App → Dapr → PostgreSQL → Response
3. [x] Tenant A and Tenant B have isolated schemas
4. [ ] Keycloak issues JWT, APISIX validates it, .NET resolves tenant

### Completed Work
- **Solution structure**: 8 projects (Host, SharedKernel, Infrastructure, Identity module, 4 test projects)
- **SharedKernel**: Entity/AuditableEntity base classes, strongly-typed IDs, Result<T> pattern, PagedResult<T>, LocalizedMessage (lockey_ enforcement), DomainException, value objects (Money, DateRange, EmailAddress, PhoneNumber), CQRS interfaces, ICacheService, ISecretProvider (generic overload), IJobScheduler, IModule, IModuleAvailability, ITenantContext, ITenantSchemaManager, IModuleMigration, JobQueues, ApiEnvelope<T>
- **Infrastructure**: BaseDbContext with DomainEventDispatcher, TenantMiddleware (401 for missing tenant, public path skip), DaprCacheService (L1+L2 with prefix invalidation + key tracking), DaprEventBus, DaprSecretProvider, HangfireJobScheduler, TenantJobFilter (tenant context capture/restore), TenantSchemaManager (PostgreSQL schema lifecycle), ValidationBehavior (all errors in Error.Details), LoggingBehavior, DatabaseTenantConfiguration, HangfireAuthFilters
- **Host**: Program.cs with Serilog, Dapr, module discovery, Hangfire dashboard (/admin/hangfire with role-based auth)
- **Identity module**: Domain entities (Tenant, Organization, User, Role, Permission, Department + join entities), strongly-typed IDs, domain events, EF configurations, PlatformDbContext (public schema), IdentityModuleMigration (seed 16 permissions + Platform Admin role)
- **Identity CQRS**: CreateTenant (with schema provisioning), CreateUser, CreateRole, UpdateTenantStatus, CreateOrganization — all with validators + lockey_ keys
- **Identity Queries**: GetTenants (paginated), GetTenantById (with modules), GetUsers (tenant-isolated), GetRoles (with permissions), GetPermissions (module filter), GetOrganizations
- **Identity API**: TenantEndpoints, UserEndpoints, RoleEndpoints, OrganizationEndpoints — all with ApiEnvelope<T> response format
- **Docker Compose**: PostgreSQL 17, Redis 7, Kafka (KRaft), Keycloak 26, MinIO, Dapr
- **Standards compliance**: 3 rounds of audit — 8 critical + 12 minor + 2 critical + 2 minor violations found and fixed
- **Tests**: 196 tests passing (SharedKernel: 64, Infrastructure: 26, Architecture: 19, Identity: 87)

---

## Phase 1: Core Platform Modules
> **Goal**: Modules that every tenant needs regardless of their business type

### 1.1 Identity & Access Management
**Spec**: [modules/identity/SPEC.md](../modules/identity/SPEC.md)
- [x] Tenant management — domain model (create, activate, suspend, terminate + domain events)
- [x] Organization management — domain model + CQRS (create, update, activate/deactivate + API endpoints)
- [x] User management — domain model (create, update profile, activate/deactivate, record login + domain events)
- [x] Role-based access control — domain model (Role, Permission, RolePermission with assign/revoke + domain events)
- [x] Permission system — module.resource.action format
- [x] Tenant management — CQRS (CreateTenant with schema provisioning, UpdateTenantStatus, GetTenants, GetTenantById) + API endpoints
- [x] User management — CQRS (CreateUser with email uniqueness, GetUsers with tenant isolation) + API endpoints
- [x] Role management — CQRS (CreateRole with permission assignment, GetRoles, GetPermissions) + API endpoints
- [x] Platform DbContext — public schema for tenant + module management
- [x] Module migration system — IdentityModuleMigration (seed permissions + Platform Admin role)
- [ ] Keycloak realm provisioning (tenant create → realm create)
- [ ] Keycloak user sync (user create → KC user create)
- [ ] Organization management — remaining endpoints (update, delete, members)
- [ ] User profile & preferences
- [ ] Login audit trail
- [ ] Module install/uninstall management API

### 1.2 Contact Management (Unified)
**Spec**: [modules/contacts/SPEC.md](../modules/contacts/SPEC.md)
- [ ] Contact CRUD (individuals & organizations)
- [ ] Contact types & tags (donor, parent, volunteer, vendor — multiple)
- [ ] 360-degree view (aggregated from all modules via IContactActivityContributor)
- [ ] Address management (multiple addresses per contact)
- [ ] Communication preferences (email, SMS, WhatsApp opt-in/out)
- [ ] Contact merge & deduplication
- [ ] Import/Export (CSV, Excel)
- [ ] Custom fields (tenant-configurable)
- [ ] KVKK/GDPR compliance (consent tracking, data export, right to delete)

### 1.3 Notification Engine
**Spec**: [modules/notifications/SPEC.md](../modules/notifications/SPEC.md)
- [ ] Email sending (SendGrid/Mailgun integration via Dapr)
- [ ] SMS sending (Twilio/Netgsm integration)
- [ ] WhatsApp Business API integration
- [ ] Notification templates (with variable substitution)
- [ ] Notification preferences per contact
- [ ] Delivery tracking (sent, delivered, opened, failed)
- [ ] Bulk sending with throttling
- [ ] Scheduled notifications
- [ ] Translation resolution (only backend component that resolves lockey_ keys)

### 1.4 Document Management
**Spec**: [modules/documents/SPEC.md](../modules/documents/SPEC.md)
- [ ] File upload/download (MinIO backend)
- [ ] Folder structure (per module, per entity)
- [ ] Digital signature (Sign module — send, sign, archive)
- [ ] Document templates with variable substitution
- [ ] Version control
- [ ] Access control (who can see/edit)
- [ ] Automatic archival (signed docs → entity's folder)

### 1.5 Portal Framework
- [ ] Portal authentication (separate from admin auth)
- [ ] Portal page builder infrastructure
- [ ] Tenant-specific branding (logo, colors, domain)
- [ ] Multi-language support (runtime language switching)
- [ ] Multi-currency display
- [ ] Module-aware navigation (only show installed module pages)

### 1.6 Reporting Engine
- [ ] Report definition (SQL-based + LINQ-based)
- [ ] Dashboard builder (widgets, charts, KPIs)
- [ ] Cross-module data aggregation
- [ ] Export (PDF, Excel, CSV)
- [ ] Scheduled reports (email delivery)
- [ ] Tenant-specific custom reports

---

## Phase 2: NGO & Foundation Modules
> **Goal**: Complete solution for non-profit/foundation organizations

### 2.1 CRM Module
**Spec**: [modules/crm/SPEC.md](../modules/crm/SPEC.md)
- [ ] Lead management (create, assign, qualify)
- [ ] Pipeline / Funnel (customizable stages per organization)
- [ ] Activities (calls, meetings, tasks linked to leads)
- [ ] Lead sources tracking (web form, referral, event)
- [ ] Contact segmentation (tags, filters, saved segments)
- [ ] Email marketing integration
- [ ] SMS marketing integration
- [ ] Campaign management
- [ ] Call center integration (click-to-call)
- [ ] Mobile-optimized views (field staff)
- [ ] CRM analytics & reports

### 2.2 Donation & Fundraising Module
**Spec**: [modules/donations/SPEC.md](../modules/donations/SPEC.md)
- [ ] Donation categories (Zakat, Orphan Fund, General, etc.)
- [ ] One-time donations (online payment)
- [ ] Recurring donations (standing orders, card-on-file)
- [ ] Donation cart (multiple items in one transaction)
- [ ] Stripe integration (international)
- [ ] iyzico/Param integration (Turkey)
- [ ] Multi-currency support (TL, USD, EUR with conversion)
- [ ] Automatic receipt generation
- [ ] Donor matching (auto-match bank transfers to donors)
- [ ] Donor portal (history, receipts, active subscriptions)
- [ ] Donation on behalf of others
- [ ] Guest donations (no account required)
- [ ] Bank transaction import & reconciliation
- [ ] Donation video linking (send video SMS to donor)
- [ ] Zakat calculator (web widget)
- [ ] Public donation page builder
- [ ] Campaign/fund tracking (% funded, goal progress)
- [ ] Donation reports (monthly, daily, YoY comparison, top campaigns)

### 2.3 Sponsorship Module
**Spec**: [modules/sponsorship/SPEC.md](../modules/sponsorship/SPEC.md)
- [ ] Sponsorship programs (student, orphan, classroom, construction)
- [ ] Sponsor-beneficiary matching
- [ ] Installment plans (monthly payments)
- [ ] Payment tracking & reminders
- [ ] Progress updates to sponsors (photos, reports, videos)
- [ ] Sponsor portal (view beneficiary, track payments, watch videos)
- [ ] Integration with donation module (auto-link payments)
- [ ] Sponsorship reports

### 2.4 Event Management Module
**Spec**: [modules/events/SPEC.md](../modules/events/SPEC.md)
- [ ] Event creation (Iftar, Sahur, Qurban, Fundraiser Dinners, Bazaars)
- [ ] Event categories and templates (seasonal reuse)
- [ ] Event registration and ticketing
- [ ] QR-based check-in and attendance tracking
- [ ] Venue and speaker management
- [ ] Sponsor history tracking ("who sponsored what, when")
- [ ] Annual organizational calendar (religious dates, holidays, fundraising events)
- [ ] Calendar sync (Outlook, Google Calendar, iCal)
- [ ] Event-based reporting
- [ ] Public event pages (portal integration)

### 2.5 Collection Box (Kumbara) Management
- [ ] Box registration (location, region, address)
- [ ] Collection tracking (amounts, dates)
- [ ] Region/area management
- [ ] Collection route planning
- [ ] Collection reports

### 2.6 Aid Package (Kumanya) Distribution
- [ ] Package donation management
- [ ] Distribution tracking
- [ ] Beneficiary management
- [ ] Distribution reports

---

## Phase 3: Education & CMS Modules
> **Goal**: Complete solution for educational institutions + multi-site CMS

### 3.1 Website & CMS Module
**Spec**: [modules/cms/SPEC.md](../modules/cms/SPEC.md)
- [ ] Multi-site support (each org gets its own site with branding)
- [ ] Page builder (block-based visual editor)
- [ ] Blog / News with editorial workflow
- [ ] SEO management (meta tags, sitemaps, structured data, Core Web Vitals)
- [ ] Form builder (contact, enrollment, volunteer forms → CRM integration)
- [ ] Theme engine (white-label per org)
- [ ] Media library (images, videos, documents)
- [ ] Navigation/menu management
- [ ] Redirect management
- [ ] Multi-language content (i18n per page)
- [ ] Mobile responsive (Next.js 16 SSR/ISR)
- [ ] Live chat / WhatsApp integration

### 3.2 Education Management Module
**Spec**: [modules/education/SPEC.md](../modules/education/SPEC.md)
- [ ] Academic year & term management
- [ ] Grade levels, classrooms, student records
- [ ] Enrollment pipeline (Application → Tour → Interview → Evaluation → Accepted → Enrolled)
- [ ] Guardian/parent linking to students
- [ ] Appointment system (school tours, parent-teacher meetings)
- [ ] Staff availability calendar
- [ ] Academic calendar (exams, holidays, special events)
- [ ] Accreditation tracking (fire drills, inspections, audits)
- [ ] Summer camp management
- [ ] Student document collection (via Documents/Sign)
- [ ] Waitlist management

### 3.3 Subscription & Billing Module
**Spec**: [modules/subscription/SPEC.md](../modules/subscription/SPEC.md)
- [ ] Subscription plans (tuition, recurring service fees)
- [ ] Billing cycles (monthly, quarterly, annual, semester)
- [ ] Automatic invoice generation on schedule
- [ ] Payment processing (Stripe, iyzico integration)
- [ ] Payment reminders (email, SMS — escalation tiers)
- [ ] Overdue tracking & late fee management
- [ ] Scholarship / discount management
- [ ] Payment portal for parents/clients
- [ ] Multi-currency billing
- [ ] Proration and plan changes
- [ ] Revenue recognition reports

### 3.4 Surveys & Feedback Module
**Spec**: [modules/surveys/SPEC.md](../modules/surveys/SPEC.md)
- [ ] Survey builder (multiple question types: single/multi choice, rating, NPS, matrix, open text)
- [ ] Survey sections and branching logic
- [ ] Distribution channels (email, SMS, portal link, QR code)
- [ ] Anonymous vs. identified responses
- [ ] Real-time response collection
- [ ] Graphical analytics and reporting
- [ ] Survey templates (reusable)
- [ ] Scheduled/recurring surveys
- [ ] Export results (PDF, Excel)

---

## Phase 4: Operations & Back-Office Modules
> **Goal**: Complete the platform with operational modules

### 4.1 Accounting & Finance Module
**Spec**: [modules/accounting/SPEC.md](../modules/accounting/SPEC.md)
- [ ] Chart of accounts (per organization, customizable)
- [ ] Double-entry journal entries
- [ ] Fiscal years and periods
- [ ] Bank account management & transaction import
- [ ] Bank reconciliation (auto-matching)
- [ ] Expense management (photo upload, multi-level approval workflow)
- [ ] Budget management & variance tracking
- [ ] Multi-currency accounting with exchange rates
- [ ] Tax rate management
- [ ] Consolidated reports (cross-organization P&L, balance sheet)
- [ ] Integration with Donations, Subscription, POS for auto-journaling

### 4.2 HR & Payroll Module
**Spec**: [modules/hr/SPEC.md](../modules/hr/SPEC.md)
- [ ] Employee records (personal info, emergency contacts, bank details)
- [ ] Department & position management
- [ ] Contract management (start/end dates, renewal alerts, digital signing)
- [ ] Payroll processing (monthly runs, deductions, benefits, four-eyes approval)
- [ ] Leave management (types, requests, approval workflow, balance tracking)
- [ ] Attendance tracking
- [ ] Shift scheduling and assignment
- [ ] Personnel document management
- [ ] Employee self-service portal

### 4.3 Point of Sale (POS) Module
**Spec**: [modules/pos/SPEC.md](../modules/pos/SPEC.md)
- [ ] Touch-friendly sales screen (tablet/phone optimized)
- [ ] Terminal and session management
- [ ] Product catalog with categories and price lists
- [ ] Cash and card payment support
- [ ] Receipt generation and printing
- [ ] Cash movement tracking (float, in/out)
- [ ] End-of-day cash reconciliation
- [ ] Event-specific POS sessions (bazaar, fundraiser)
- [ ] Offline capability (IndexedDB sync)
- [ ] Inventory integration (auto stock deduction)
- [ ] Accounting integration (auto journal entries)

### 4.4 Fleet Management Module
**Spec**: [modules/fleet/SPEC.md](../modules/fleet/SPEC.md)
- [ ] Vehicle records (plate, model, VIN, registration details)
- [ ] Vehicle assignment tracking (who has which vehicle)
- [ ] Insurance policy tracking (expiry alerts, renewal workflow)
- [ ] Maintenance scheduling (km-based and time-based)
- [ ] Maintenance record keeping
- [ ] Fuel consumption logging (with anomaly detection)
- [ ] Vehicle inspection tracking
- [ ] Vehicle document management
- [ ] Cost tracking per vehicle (fuel, maintenance, insurance, tolls)
- [ ] Fleet dashboard and reports

### 4.5 Inventory & Asset Management Module
**Spec**: [modules/inventory/SPEC.md](../modules/inventory/SPEC.md)
- [ ] Warehouse management (per organization)
- [ ] Location hierarchy within warehouses
- [ ] Product catalog with categories
- [ ] Stock movements (receive, transfer, ship — with approval workflow)
- [ ] Fixed asset tracking (barcode/QR scanning)
- [ ] Asset assignment and accountability
- [ ] Stocktaking / inventory count with variance reconciliation
- [ ] Low stock alerts
- [ ] Supplier management
- [ ] Stock reports and dashboards

### 4.6 Project Management Module
**Spec**: [modules/projects/SPEC.md](../modules/projects/SPEC.md)
- [ ] Project creation with milestones
- [ ] Task management (Kanban board with WIP limits)
- [ ] Task comments and attachments
- [ ] Time tracking (per task, per member)
- [ ] Project team / member management
- [ ] Project budgeting and cost tracking
- [ ] Construction cost center tracking (materials, labor, subcontractors)
- [ ] Meeting notes with action item conversion to tasks
- [ ] Subcontractor contract management (Documents/Sign integration)
- [ ] Labels and filtering
- [ ] Project dashboard and Gantt views
- [ ] Accounting integration (cost journal entries)

---

## Cross-Phase: Continuous Improvements
> These run throughout all phases

- [ ] Performance optimization & load testing
- [ ] Security audits & penetration testing
- [ ] Accessibility (WCAG 2.1 AA)
- [ ] Documentation (user guides, API docs, developer docs)
- [ ] Automated testing (unit, integration, e2e)
- [ ] Mobile app (React Native — Phase 3+)
- [ ] Marketplace (3rd party module publishing — Phase 4+)

---

## Module Summary Matrix

| Module | Phase | Dependencies (Required) | Dependencies (Optional) | Key Integration Points |
|--------|-------|------------------------|------------------------|----------------------|
| Identity & Access | Core | — | — | Keycloak, APISIX |
| Contact Management | Core | identity | — | 360-view contributors |
| Notification Engine | Core | identity, contacts | — | All modules (event-driven) |
| Document Management | Core | identity | contacts | MinIO, Sign |
| CRM | Phase 2 | contacts, notifications | — | Web forms, Events |
| Donations | Phase 2 | contacts, notifications, documents | — | Stripe, iyzico, Bank import |
| Sponsorship | Phase 2 | contacts, donations, notifications | — | Donor portal |
| Events | Phase 2 | identity, contacts, notifications | documents, crm, donations | Calendar sync, QR check-in |
| Education | Phase 3 | crm, contacts, documents, notifications | subscription | Enrollment pipeline |
| Subscription | Phase 3 | identity, contacts, notifications | — | Stripe, iyzico |
| Website & CMS | Phase 3 | identity, notifications | contacts, crm, donations | Next.js 16 SSR |
| Surveys | Phase 3 | identity, contacts, notifications | — | Distribution channels |
| Accounting | Phase 4 | identity, contacts | hr, documents, notifications | Donations, Subscription, POS |
| HR & Payroll | Phase 4 | identity, contacts, notifications, documents | accounting | Contract Sign |
| Point of Sale | Phase 4 | identity, contacts, notifications | inventory, accounting | Offline sync |
| Fleet | Phase 4 | identity, contacts, notifications, documents | — | Maintenance alerts |
| Inventory | Phase 4 | identity, contacts, notifications | documents | POS, barcode scan |
| Projects | Phase 4 | identity, contacts, notifications | documents, accounting | Cost tracking |
