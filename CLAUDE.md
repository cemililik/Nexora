# Nexora - Claude Code Instructions

## Project Overview
Nexora is a modular, multi-tenant enterprise platform built with .NET 10, PostgreSQL, React 19, and Next.js 16. It follows a Modular Monolith architecture with Clean Architecture per module.

## Mandatory Standards

### CRITICAL: Always Follow These Standards
Before writing ANY code or documentation, you MUST read and strictly follow.
All standards live in `docs/standards/` (lowercase, canonical). The legacy UPPERCASE
files have been archived to `docs/_archive/standards-legacy/`.

1. **Architectural Principles**: `docs/standards/architectural-principles.md`
   - SOLID, Modular Monolith, Clean Architecture per module
   - Result pattern, module boundaries, observability, infrastructure primitives

2. **Code Style**: `docs/standards/code-style.md`
   - C# / TypeScript naming, file-scoped namespaces, primary constructors
   - API endpoint convention: `/api/v{version}/{module}/{resource}`
   - ApiEnvelope<T> contract, Conventional Commits message style

3. **Testing**: `docs/standards/testing.md`
   - Three test tiers (unit, integration, architecture)
   - Naming: `Method_Scenario_ExpectedResult`
   - Mock policy and current coverage counts

4. **Documentation Style**: `docs/standards/documentation-style.md`
   - Mermaid mandate — all diagrams embedded inline
   - ADR immutability, required module-spec diagrams
   - Linking and template rules

5. **Commit Style**: `docs/standards/commit-style.md`
   - Conventional Commits, trailer format, branch flow, squash-merge policy
   - Release/SemVer rules

6. **Localization**: `docs/standards/localization.md`
   - **ZERO hardcoded user-facing strings** — backend or frontend
   - `lockey_{scope}_{context}_{descriptor}` format
   - Backend returns `lockey_` keys in responses, NEVER translated strings
   - FluentValidation `.WithMessage()`, DomainException, and Result.Success/Failure MUST use lockey keys

7. **Permissions**: `docs/standards/permissions.md`
   - `{module}.{resource}.{action}` format, Platform vs Tenant scope, seeding rules
   - Built-in roles (Platform Admin / Tenant Admin / Tenant User / Portal User) and per-module matrix

8. **Audit Coverage**: `docs/standards/audit-coverage.md`
   - Per-module operation-class audit matrix (MUST/SHOULD/MAY)

9. **Multi-Currency**: `docs/standards/multi-currency.md`
   - Money value object, exchange rate service, cross-module usage, display rules

10. **UX/UI**: `docs/standards/ux-ui.md`
    - Tab-based layout mandate (custom underline tabs, not Radix/shadcn Tabs), max 5 tabs
    - Shared component inventory (§9), empty/loading states, accessibility

11. **Code Review**: `docs/standards/code-review.md`
    - 10-category review checklist, severity, zero-tolerance rules

12. **Security Review**: `docs/standards/security-review.md`
    - OWASP lens, multi-tenant isolation, secrets, CVEs, PII handling

13. **Schema Migration**: `docs/standards/schema-migration.md`
    - Migration-free schema evolution: `ApplySchemaUpdatesAsync` pattern in `DevelopmentSeed.cs`
    - Idempotency rules (`IF NOT EXISTS`), C# → PostgreSQL type map
    - No EF Core migration files in this project

14. **Module System** (architecture, not a standard): `docs/architecture/MODULE_SYSTEM.md`
    - Modules are true plugins — installable/removable per tenant at runtime
    - `IModule` interface, explicit dependency declaration
    - Cross-module comms via integration events (Kafka) or SharedKernel interfaces
    - Module tables prefixed `{module}_{table}` in tenant schema
    - UI loaded dynamically based on installed modules

### Observability & Error Handling (rules inline)

- Structured logging with Serilog + `ILogger<T>` — PascalCase named parameters, no string interpolation
- Two-tier error model: `Result.Failure()` for expected errors, exceptions for unexpected
- `DomainException` only from domain entities — handlers use `Result.Failure()`
- `GlobalExceptionHandler` middleware catches all unhandled exceptions → standard `ApiEnvelope` response
- OpenTelemetry for distributed tracing and metrics; custom `ActivitySource` for external calls / jobs
- Module-specific metrics via `System.Diagnostics.Metrics` (Meter/Counter/Histogram)
- Health checks: `/health/live`, `/health/ready`, `/health/startup`
- `CorrelationId` propagated across the entire request chain
- Command handlers MUST log success (Information) and business rule failures (Warning)
- **NEVER** log secrets, passwords, tokens, or PII
- **NEVER** use `catch(Exception)` in module code — only in `GlobalExceptionHandler` and `NexoraJob`

### Frontend & API Integration (rules inline — no standalone doc yet)

- TypeScript strict, functional components, no `any`
- State: TanStack Query (server) + Zustand (client) + React Hook Form (forms)
- Styling: Tailwind CSS 4 + shadcn/ui, `cn()` utility
- Testing: Vitest + React Testing Library
- API responses are `ApiEnvelope<T>` — always unwrap `data` field
- Error messages are `lockey_` keys — resolve with `t(key, meta)` and the `useApiError` hook
- File upload: presigned URL pattern — no direct multipart uploads
- Canonical standards: `docs/standards/` — read these first.
- Archived/legacy (reference only, may be out of date): `docs/_archive/standards-legacy/FRONTEND_STANDARDS.md` and `docs/_archive/standards-legacy/API_INTEGRATION_STANDARDS.md`. Treat as historical context, not normative.
- Integration guide: `docs/guides/API_INTEGRATION_GUIDE.md`

### Infrastructure (rules inline — no standalone doc yet)

- Canonical standards: `docs/standards/` — read these first when an item has a canonical doc.
- Archived/legacy (reference only, may be out of date): `docs/_archive/standards-legacy/INFRASTRUCTURE_STANDARDS.md`. Treat as historical context until a canonical infrastructure standard lands.

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
│   ├── components/
│   │   ├── ui/                     # shadcn/ui primitives (Button, Input, Dialog, Select...)
│   │   ├── data/                   # DataTable, SearchInput, SearchableDropdown, FormField, TextareaWithCounter
│   │   ├── feedback/               # EmptyState, TabContentSkeleton, LoadingSkeleton, ConfirmDialog, ErrorBoundary
│   │   └── layout/                 # AppLayout, Sidebar, Topbar, Breadcrumbs
│   ├── hooks/                      # useAuth, usePermissions, usePagination, useUnsavedChangesGuard, useUndoableDelete
│   ├── lib/                        # api client, i18n config, utils, stores (Zustand)
│   └── types/                      # ApiEnvelope, PagedResult, auth types
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
- **Development schema evolution** — no EF Core migration files yet. Tables are created on first startup via `IRelationalDatabaseCreator`; incremental changes go into `ApplySchemaUpdatesAsync` in `DevelopmentSeed.cs` as idempotent SQL (`ADD COLUMN IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`). **`DevelopmentSeed` is guarded by `app.Environment.IsDevelopment()` and must never run against production.** See [`docs/standards/schema-migration.md`](docs/standards/schema-migration.md).
- **Production schema evolution strategy: TBD** — tracked in a forthcoming ADR. Until that ADR lands, production DDL must not travel through `ApplySchemaUpdatesAsync`; the archived reference at [`docs/_archive/standards-legacy/INFRASTRUCTURE_STANDARDS.md`](docs/_archive/standards-legacy/INFRASTRUCTURE_STANDARDS.md) is the interim pointer.
- Strongly-typed IDs (never raw Guid/int in domain).
- Schema changes are additive-only (no `DROP COLUMN`, no type/nullability tightening) in both dev and production — the dev mechanism enforces this, and the production ADR will inherit the same rule.

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

**Canonical standards: `docs/standards/`** — read these first. A dedicated
infrastructure standard has not yet landed; until it does, the archived file
at `docs/_archive/standards-legacy/INFRASTRUCTURE_STANDARDS.md` is historical
reference only (may be out of date).

### Cache
- Use **only** `ICacheService` for caching — never `IDistributedCache`, `IMemoryCache`, or `DaprClient` directly
- Cache key format (module perspective): `{module}:{entity}:{identifier}` — tenant ID prefix is **automatically enforced** by `DaprCacheService` via `ITenantContextAccessor`. Modules do NOT include tenant ID in keys; the infrastructure layer prepends it transparently (actual stored key: `{tenantId}:{module}:{entity}:{identifier}`)
- L1 (in-memory, 2 min) → L2 (Redis via Dapr, 15 min) → Database
- Cache-aside pattern: `GetOrSetAsync` for reads, explicit invalidation on writes

### Background Jobs
- Use **Hangfire** for all background/scheduled work
- All jobs extend `NexoraJob<TParams>` base class (tenant-aware, logged, traced)
- Job naming: `{module}:{action-descriptor}` (e.g., `donations:recurring-charge`)
- 4 queues: `critical` (payments), `default` (normal), `bulk` (mass ops), `maintenance` (cleanup)
- Jobs MUST be idempotent (Hangfire retries automatically)
- Register recurring jobs in `IModule.ConfigureJobs()` — use expression pattern: `job => job.RunAsync(params, ct)`
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
- DELETE endpoints return `200 OK` with `ApiEnvelope.Success(result.Message)` — not `204 NoContent`
- Query handlers MUST use `.AsNoTracking()` for all read-only queries
- `Entity<T>.Equals()` is null-safe and type-safe — callers should rely on it (or `==`/`!=` operators) without manual null checks
- `AuditableEntity.MarkAsDeleted()` MUST validate parameters (non-null deletedBy, valid timestamp)
- **Soft Delete**: All `AuditableEntity<T>` entities use soft delete automatically:
  - `dbContext.Remove(entity)` → auto-converts to `IsDeleted=true` (never hard deletes)
  - All queries auto-filter `WHERE IsDeleted = false` via global query filter
  - Use `IgnoreQueryFilters()` only for admin audit views
  - `IsActive` (temporary) ≠ `IsDeleted` (permanent) — different concepts
  - Hard delete is allowed ONLY for: GDPR compliance, join-table reconciliation (removing orphaned many-to-many rows), and uninstall/orphan cleanup (removing data for deprovisioned modules or tenants)

## When Writing Frontend Code

**Canonical standards: `docs/standards/`** — read these first.
**UX/UI standard**: `docs/standards/ux-ui.md` (canonical).
**Integration guide**: `docs/guides/API_INTEGRATION_GUIDE.md`.
**Archived/legacy (reference only, may be out of date)**: `docs/_archive/standards-legacy/FRONTEND_STANDARDS.md` and `docs/_archive/standards-legacy/API_INTEGRATION_STANDARDS.md`. Treat as historical context, not normative.

### UX/UI Layout Rules
- Detail pages MUST use **custom underline tab layout** (`<button>` with `border-b-2`) — NOT shadcn/Radix Tabs, NOT card-based side-by-side
- Tab state via `useState` (not URL params). See ContactDetailPage/DocumentDetailPage as reference.
- **Max 5 tabs** per detail page. Consolidate small sections into Overview tab (e.g., Tags, Custom Fields).
- Tab content MUST show `TabContentSkeleton` while loading. Tab switch MUST reset scroll position.
- Header: entity name + badges grouped left, actions right. Keep related info close — don't spread across full width.
- List pages: filters/search/pagination in URL params, DataTable with clickable rows
- **Every list page MUST have SearchInput** + shadcn Select filters (never native `<select>`)
- Empty states MUST use `EmptyState` component (icon + title + description + CTA)
- Edit forms MUST use `useUnsavedChangesGuard(isDirty)` to prevent accidental navigation
- Form fields MUST use `FormField` wrapper (label + required `*` + hint + error)
- Grid layouts MUST use responsive breakpoints: `grid-cols-1 sm:grid-cols-2` (never bare `grid-cols-2`)
- Refer to `docs/standards/ux-ui.md` §9 for full shared component inventory

### Pre-Commit Checklist: ALWAYS Run Linter Before Committing
**CRITICAL**: Any changes in `src/Clients/` (nexora-admin or nexora-portal) MUST pass linting before commit.

```bash
# For nexora-admin:
cd src/Clients/nexora-admin && npm run lint

# For nexora-portal:
cd src/Clients/nexora-portal && npm run lint
```

**DO NOT commit if linter fails.** The CI/CD pipeline will reject the PR.

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
- Follow the module spec template in `docs/standards/documentation-style.md`
- Use ADR template for architecture decisions
- Keep CHANGELOG.md updated with every release

## Language
- Code: English (all identifiers, comments, documentation)
- User-facing content: Multi-language support (i18n)
- Internal team docs: English or Turkish (context-dependent)
