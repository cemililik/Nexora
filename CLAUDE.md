# Nexora - Claude Code Instructions

## Project Overview
Nexora is a modular, multi-tenant enterprise platform built with .NET 10, PostgreSQL, React 19, and Next.js 16. It follows a Modular Monolith architecture with Clean Architecture per module.

## Mandatory Standards

### CRITICAL: Always Follow These Standards
Before writing ANY code or documentation, you MUST read and strictly follow:

1. **Coding Standards**: `docs/standards/CODING_STANDARDS.md`
   - SOLID principles, CQRS pattern, strongly-typed IDs
   - C# naming conventions (_camelCase for private fields, PascalCase for everything else)
   - File-scoped namespaces, primary constructors for DI
   - Rich domain model (behavior on entities, not anemic)
   - Result pattern for expected failures, exceptions for unexpected
   - API conventions: `/api/v{version}/{module}/{resource}`
   - Conventional Commits for git messages
   - Test naming: `Method_Scenario_ExpectedResult`

2. **Documentation Standards**: `docs/standards/DOCUMENTATION_STANDARDS.md`
   - All diagrams MUST use Mermaid (renders natively in GitHub)
   - Diagrams must be embedded inline in markdown, not as separate image files
   - ADRs are immutable once accepted
   - Every module spec requires: ER diagram, state diagrams, sequence diagrams, component diagram, integration diagram
   - API docs auto-generated from XML comments

3. **Release Standards**: `docs/standards/RELEASE_STANDARDS.md`
   - Semantic Versioning (SemVer)
   - GitHub Flow branching strategy
   - Conventional Commits required
   - Squash merge to main

4. **Localization Standards**: `docs/standards/LOCALIZATION_STANDARDS.md`
   - **ZERO hardcoded user-facing strings** — backend or frontend
   - All messages use `lockey_{scope}_{context}_{descriptor}` format
   - Backend returns `lockey_` keys in responses, NEVER translated strings
   - Frontend resolves keys via `react-i18next` (admin) / `next-intl` (portal)
   - FluentValidation `.WithMessage()` MUST use `lockey_` keys
   - DomainException MUST use `lockey_` keys
   - Result.Failure/Success MUST use `LocalizedMessage.Of("lockey_...")`

5. **Module System**: `docs/architecture/MODULE_SYSTEM.md`
   - Modules are true plugins — installable/removable per tenant at runtime
   - Every module implements `IModule` interface
   - Modules declare their dependencies explicitly
   - Cross-module communication via integration events (Kafka) or SharedKernel interfaces
   - Module tables prefixed: `{module}_{table}` in tenant schema
   - Module UI loaded dynamically based on tenant's installed modules

## Solution Structure
```
src/
├── Nexora.Host/                    # Main ASP.NET host
├── Nexora.SharedKernel/            # Shared types, base classes
├── Nexora.Infrastructure/          # Cross-cutting: EF, Caching, Messaging
├── Modules/
│   ├── Nexora.Modules.Identity/    # Auth, tenants, orgs, users, RBAC
│   ├── Nexora.Modules.Contacts/    # Unified contact registry
│   ├── Nexora.Modules.CRM/         # Leads, pipelines, campaigns
│   ├── Nexora.Modules.Donations/   # Online giving, recurring, receipts
│   └── ... (one project per module)
└── Clients/
    ├── nexora-admin/               # React 19 admin dashboard
    └── nexora-portal/              # Next.js 16 public portal
```

## Module Architecture (Clean Architecture per module)
```
Nexora.Modules.{ModuleName}/
├── Domain/              # Entities, Value Objects, Domain Events, Interfaces
├── Application/         # Commands, Queries (CQRS via MediatR), DTOs, Validators
├── Infrastructure/      # EF DbContext, Repositories, External Services
└── Api/                 # Endpoints, Middleware
```

## Key Architecture Rules

### Module Boundaries
- Modules MUST NOT directly reference other modules' internal types
- Cross-module communication: MediatR notifications (in-process) or Dapr pub/sub (Kafka)
- Each module has its OWN DbContext — no shared tables across module boundaries
- Shared types live in `Nexora.SharedKernel`

### Multi-Tenancy
- Schema-per-tenant in PostgreSQL
- Tenant resolved from JWT `tenant_id` claim
- Organization filtering via `organization_id` column within tenant schema
- EF Core global query filters enforce org-level isolation

### Authentication & Authorization
- Keycloak (realm-per-tenant, OIDC)
- JWT validated at APISIX gateway layer
- Permission-based RBAC: `{module}.{resource}.{action}`
- Organization-scoped permissions

### Database
- PostgreSQL 17, schema-per-tenant
- EF Core Code-First migrations
- Strongly-typed IDs (never raw Guid/int in domain)
- Migrations are additive-only in production

## Tech Stack Quick Reference
- Backend: .NET 10, ASP.NET Core, EF Core, MediatR, FluentValidation, Mapster
- Database: PostgreSQL 17, Redis (cache), Kafka (events)
- Auth: Keycloak, APISIX (gateway)
- Infrastructure: Dapr, HashiCorp Vault, MinIO
- Frontend: React 19 + TypeScript (admin), Next.js 16 (portal), Tailwind CSS 4, shadcn/ui
- DevOps: Docker, Kubernetes, Helm, GitHub Actions
- Observability: OpenTelemetry, Grafana, Loki, Tempo

## When Writing Code
- Use file-scoped namespaces
- Use primary constructors for dependency injection
- Use sealed classes by default
- Use records for DTOs and commands/queries
- Every command MUST have a FluentValidation validator
- Use the Result<T> pattern for handler returns
- Add XML documentation on all public types and methods
- Write architecture tests to enforce module boundaries
- **NEVER** hardcode user-facing strings — use `lockey_` keys everywhere:
  - `Result.Success(data, LocalizedMessage.Of("lockey_module_action_success"))`
  - `Result.Failure<T>(LocalizedMessage.Of("lockey_error_something_wrong"))`
  - `throw new DomainException("lockey_module_business_rule_violated")`
  - `RuleFor(x => x.Field).NotEmpty().WithMessage("lockey_validation_required")`
- Module code must implement `IModule` interface
- Module tables must be prefixed: `{modulename}_{tablename}`
- No direct references to other modules — use SharedKernel interfaces or integration events

## When Writing Frontend Code
- **NEVER** write raw text in JSX — always use translation function:
  - React: `const { t } = useTranslation('module'); ... {t('lockey_module_label')}`
  - Next.js: `const t = useTranslations('module'); ... {t('lockey_module_label')}`
- Translation files organized per module: `locales/{lang}/{module}.json`
- All `lockey_` keys must exist in both `en` and `tr` translation files minimum
- Support RTL for Arabic (use Tailwind RTL utilities)
- Module UI loaded dynamically — use lazy imports and module manifests

## When Writing Documentation
- Always use Mermaid for diagrams — embedded inline in markdown
- Follow the module spec template in `docs/standards/DOCUMENTATION_STANDARDS.md`
- Use ADR template for architecture decisions
- Keep CHANGELOG.md updated with every release

## Language
- Code: English (all identifiers, comments, documentation)
- User-facing content: Multi-language support (i18n)
- Internal team docs: English or Turkish (context-dependent)
