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

5. **Observability & Error Handling Standards**: `docs/standards/OBSERVABILITY_STANDARDS.md`
   - Structured logging with Serilog + `ILogger<T>` — PascalCase named parameters, no string interpolation
   - Two-tier error model: `Result.Failure()` for expected errors, exceptions for unexpected
   - `DomainException` only from domain entities — handlers use `Result.Failure()`
   - `GlobalExceptionHandler` middleware catches all unhandled exceptions → standard `ApiEnvelope` response
   - OpenTelemetry for distributed tracing and metrics
   - Custom `ActivitySource` for external service calls and job execution
   - Module-specific metrics via `System.Diagnostics.Metrics` (Meter/Counter/Histogram)
   - Health checks: `/health/live`, `/health/ready`, `/health/startup`
   - `CorrelationId` propagated across entire request chain
   - Command handlers MUST log success (Information) and business rule failures (Warning)
   - **NEVER** log secrets, passwords, tokens, or PII
   - **NEVER** use `catch(Exception)` in module code — only in GlobalExceptionHandler and NexoraJob

6. **Frontend Standards**: `docs/standards/FRONTEND_STANDARDS.md`
   - TypeScript strict, functional components, no `any`
   - State: TanStack Query (server) + Zustand (client) + React Hook Form (forms)
   - Styling: Tailwind CSS 4 + shadcn/ui, `cn()` utility
   - Testing: Vitest + React Testing Library
   - API integration patterns and query key conventions
   - Module manifest structure for dynamic UI loading

7. **API Integration Guide**: `docs/guides/API_INTEGRATION_GUIDE.md`
   - ApiEnvelope<T> response format and TypeScript types
   - Pagination with PagedResult<T>
   - Error handling (status codes → user actions)
   - TanStack Query patterns (query keys, hooks, cache invalidation)
   - Authentication flow (Keycloak JWT)
   - File upload (presigned URL pattern)

8. **Module System**: `docs/architecture/MODULE_SYSTEM.md`
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

## Frontend Module Structure
```
nexora-admin/src/                   # (same pattern for nexora-portal)
├── app/                            # Application shell, providers, router
├── modules/                        # Feature modules (mirrors backend modules)
│   ├── identity/                   # components/, hooks/, pages/, types/, manifest.ts
│   ├── contacts/
│   ├── notifications/
│   └── documents/
├── shared/                         # Shared across all modules
│   ├── components/ui/              # shadcn/ui primitives
│   ├── hooks/                      # useAuth, usePermissions, usePagination
│   ├── lib/                        # api client, i18n config, utils
│   └── types/                      # ApiEnvelope, PagedResult, auth types
├── layouts/                        # AppLayout, Sidebar, Topbar
└── locales/{en,tr}/                # Translation files per module
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
- Database: PostgreSQL 17, Redis (cache via Dapr State Store), Kafka (events via Dapr Pub/Sub)
- Auth: Keycloak, APISIX (gateway)
- Infrastructure: Dapr (state, secrets, pub/sub), HashiCorp Vault (secrets), MinIO (files)
- Jobs: Hangfire (PostgreSQL-backed)
- Frontend: React 19 + TypeScript (admin), Next.js 16 (portal), Tailwind CSS 4, shadcn/ui
- DevOps: Docker, Kubernetes, Helm, GitHub Actions
- Observability: OpenTelemetry, Grafana, Loki, Tempo

## Infrastructure Standards
**Full spec**: `docs/standards/INFRASTRUCTURE_STANDARDS.md`

### Cache
- Use **only** `ICacheService` for caching — never `IDistributedCache`, `IMemoryCache`, or `DaprClient` directly
- Cache key format: `{module}:{tenant}:{entity}:{identifier}`
- Tenant ID in cache key is **mandatory** — cross-tenant cache leak is a critical security issue
- L1 (in-memory, 2 min) → L2 (Redis via Dapr, 15 min) → Database
- Cache-aside pattern: `GetOrSetAsync` for reads, explicit invalidation on writes

### Background Jobs
- Use **Hangfire** for all background/scheduled work
- All jobs extend `NexoraJob<TParams>` base class (tenant-aware, logged, traced)
- Job naming: `{module}:{action-descriptor}` (e.g., `donations:recurring-charge`)
- 4 queues: `critical` (payments), `default` (normal), `bulk` (mass ops), `maintenance` (cleanup)
- Jobs MUST be idempotent (Hangfire retries automatically)
- Register recurring jobs in `IModule.ConfigureJobs()`
- Max job duration: 10 minutes. Longer tasks must be split into batches

### Secrets
- Use **only** `ISecretProvider` for secrets — backed by Dapr Secret Store (Vault in prod)
- Secret naming: `nexora/{category}/{name}` (e.g., `nexora/stripe/api-key`)
- **NEVER** put secrets in appsettings.json, env vars, or source code
- **NEVER** log secret values

### Configuration
- 5-layer hierarchy: appsettings.json → env-specific → env vars → Dapr secrets → tenant DB config
- Use `IOptions<T>` / `IOptionsMonitor<T>` for strongly-typed module config
- Tenant-specific config via `ITenantConfiguration` (stored in DB, overrides platform defaults)
- Every config key MUST have a sensible default value
- Validate config on startup with `IValidateOptions<T>`

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
- Command handlers MUST inject `ILogger<T>` and log:
  - `Information` on successful entity creation/update/deletion
  - `Warning` on expected business rule failures (before returning `Result.Failure()`)
  - `Error` on external service failures (Keycloak, payment, etc.)
- Query handlers: log `Debug` for not-found, `Warning` for slow queries (>500ms)
- Use structured logging: `logger.LogInformation("Tenant {TenantId} created", id)` — no string interpolation
- DomainException ONLY from domain entities — handlers return `Result.Failure()` instead
- Never use `catch(Exception)` in module code

## When Writing Frontend Code
**Full spec**: `docs/standards/FRONTEND_STANDARDS.md`
**API guide**: `docs/guides/API_INTEGRATION_GUIDE.md`

### General Rules
- TypeScript strict mode — **NEVER** use `any`
- Functional components only — no class components
- **NEVER** write raw text in JSX — always use translation function:
  - React: `const { t } = useTranslation('module'); ... {t('lockey_module_label')}`
  - Next.js: `const t = useTranslations('module'); ... {t('lockey_module_label')}`
- Translation files organized per module: `locales/{lang}/{module}.json`
- All `lockey_` keys must exist in both `en` and `tr` translation files minimum

### State Management
- **Server state**: TanStack Query v5 — `useQuery` for reads, `useMutation` for writes
- **Client state**: Zustand — minimal stores (auth, theme, sidebar only)
- **Form state**: React Hook Form + Zod validation
- **URL state**: query params for search, filters, pagination
- **NEVER**: Redux, Context for frequently changing data, direct fetch/useEffect for API calls

### Styling
- Tailwind CSS 4 utility-first + shadcn/ui components
- Use `cn()` utility for conditional classes (clsx + tailwind-merge)
- RTL support for Arabic: Tailwind `rtl:` prefix utilities
- **NEVER**: inline `style={}`, CSS modules, styled-components

### API Integration
- Use custom hooks per resource: `useContacts()`, `useCreateContact()`
- API responses are `ApiEnvelope<T>` — always unwrap `data` field
- Error messages are `lockey_` keys — resolve with `t(key, meta)`
- Validation errors: set field-level errors on form via `setError()`
- Show success/error toasts with translated messages
- Invalidate queries after mutations

### Module System
- Each module exposes a `ModuleManifest` (routes, navigation, permissions)
- Module UI loaded dynamically — use `lazy()` imports
- Check installed modules before rendering: `useInstalledModules()`
- Permission guard: `hasPermission('module.resource.action')`

### Component Conventions
- Component files: `PascalCase.tsx` (e.g., `ContactList.tsx`)
- Hooks: `use{Name}.ts` (e.g., `useContacts.ts`)
- Props interface: `{ComponentName}Props` in same file
- Shared components: named export; page components: default export
- Tests co-located: `ContactList.test.tsx` next to `ContactList.tsx`

### Security
- **NEVER** use `dangerouslySetInnerHTML` (XSS risk)
- **NEVER** store tokens in `localStorage` — use httpOnly cookies or secure memory
- UI permission checks are for UX only — backend enforces authorization
- All `VITE_` and `NEXT_PUBLIC_` env vars are public — never put secrets there

## When Writing Documentation
- Always use Mermaid for diagrams — embedded inline in markdown
- Follow the module spec template in `docs/standards/DOCUMENTATION_STANDARDS.md`
- Use ADR template for architecture decisions
- Keep CHANGELOG.md updated with every release

## Language
- Code: English (all identifiers, comments, documentation)
- User-facing content: Multi-language support (i18n)
- Internal team docs: English or Turkish (context-dependent)
