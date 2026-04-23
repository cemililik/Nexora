# Architectural Principles

Derives from: [ADR-001 Modular Monolith](../decisions/ADR-001-modular-monolith.md),
[ADR-002 Multi-Tenancy](../decisions/ADR-002-multi-tenancy.md),
[ADR-014 Distributed Consistency Patterns](../decisions/ADR-014-distributed-consistency-patterns.md),
[ADR-016 Module Tier Classification](../decisions/ADR-016-module-tier-classification.md).

Source content: CLAUDE.md §Key Architecture Rules, §Infrastructure Standards; legacy
`CODING_STANDARDS.md` §1–5, `INFRASTRUCTURE_STANDARDS.md`, `OBSERVABILITY_STANDARDS.md`.

## 1. Foundational Principles

- **SOLID** — mandatory, not optional.
- **YAGNI** — no abstractions for hypothetical futures.
- **DRY** — only when duplication shares a reason to change.
- **Favor composition over inheritance.**
- **Make illegal states unrepresentable** via the type system.

## 2. Modular Monolith

Nexora is a single deployable process composed of isolated modules (see
[ADR-001](../decisions/ADR-001-modular-monolith.md)). Every feature ships inside a module.

### Module Boundaries

- Modules MUST NOT directly reference other modules' internal types.
- Cross-module communication: MediatR notifications (in-process) **or** integration events
  over Dapr pub/sub (Kafka).
- Each module owns its `DbContext`. No shared tables across module boundaries.
- Module tables are prefixed `{module}_{table}` in the tenant schema.
- Shared types live in `Nexora.SharedKernel`.

### Module Contract (`IModule`)

Every module MUST:

1. Implement `IModule`.
2. Declare its `Dependencies` on other modules explicitly.
3. Register endpoints via `MapEndpoints()`.
4. Register event handlers via `ConfigureEventHandlers()`.
5. Implement `OnInstallAsync()` / `OnUninstallAsync()`.
6. Register permissions via `OnStartupAsync()` — see [permissions.md](permissions.md).
7. Contribute to shared views via `IContactActivityContributor` where relevant.

## 3. Clean Architecture (per module)

```
Nexora.Modules.{ModuleName}/
├── Domain/            # Entities, Value Objects, Domain Events, Interfaces
├── Application/       # Commands, Queries (CQRS via MediatR), DTOs, Validators
├── Infrastructure/    # EF DbContext, Repositories, External Services
└── Api/               # Endpoints, Middleware
```

Inner layers know nothing about outer layers. Domain holds invariants; Application orchestrates
use cases; Infrastructure implements ports; Api adapts HTTP.

## 4. Tier Dependency Rules

Per [ADR-016](../decisions/ADR-016-module-tier-classification.md):

- **Tier 1 (Core)** — identity, contacts, notifications, documents, audit, reporting,
  portal-framework, admin-dashboard. May be depended on by any tier.
- **Tier 2 (Enterprise)** — crm, finance, subscription, projects, hr. May depend on Tier 1 only.
- **Tier 3a (NGO)** — fundraising, events-ngo. May depend on Tier 1 and Tier 2.
- **Tier 3b (Education)** — education. May depend on Tier 1 and Tier 2.
- **Tier 4 (Deferred)** — archived specs; not installable in current platform.

Higher tiers MUST NOT be referenced by lower tiers. Architecture tests enforce this
(see [testing.md](testing.md) §Architecture Tests).

## 5. Rich Domain Model

- Behavior lives on entities, not in services — no anemic domain.
- Aggregate roots raise domain events via `AddDomainEvent()`. Handlers MUST NOT publish
  domain events directly.
- Use **strongly-typed IDs** (never raw `Guid`/`int` in domain).
- Use **records** for value objects and DTOs.
- `DomainException` originates **only** from domain entities for invariant violations; it
  carries a `lockey_` key. Application handlers use `Result.Failure()` instead.

## 6. CQRS via MediatR

- Every write is a `Command` implementing `IRequest<Result<TResponse>>`.
- Every read is a `Query` implementing `IRequest<TResponse>` — read paths MUST use
  `.AsNoTracking()`.
- Every command MUST have a `FluentValidation` validator. Validator messages MUST be
  `lockey_` keys (see [localization.md](localization.md)).
- Handlers are `sealed` and use primary-constructor DI.

## 7. Result Pattern & Error Model

Two-tier error model:

- **Expected errors** → `Result.Failure<T>(LocalizedMessage.Of("lockey_..."))`.
- **Unexpected errors** → exceptions, caught only by `GlobalExceptionHandler` middleware
  and `NexoraJob`.

Rules:

- **Never** use `catch(Exception)` in module code.
- `DELETE` endpoints return `200 OK` with `ApiEnvelope.Success(result.Message)` — not `204`.
- All user-facing messages flow through `LocalizedMessage` with `lockey_` keys.

## 8. Multi-Tenancy

Per [ADR-002](../decisions/ADR-002-multi-tenancy.md):

- **Schema-per-tenant** in PostgreSQL.
- Tenant resolved from JWT `tenant_id` claim via `ITenantContextAccessor` — **never** from
  request body or query string.
- Organization filtering via `organization_id` column inside the tenant schema, enforced by
  EF Core global query filters.
- Strongly-typed IDs are tenant-scoped where semantically meaningful.

## 9. Distributed Consistency

See [ADR-014](../decisions/ADR-014-distributed-consistency-patterns.md). When a command writes
to local DB **and** an external service, pick a tier before coding:

- **Tier 1** — No external calls → standard EF Core.
- **Tier 2A** — External is mirror of DB → DB-first, external call after, failure non-fatal.
- **Tier 2B** — External returns an ID you must store → external-first + compensating delete
  on DB failure.
- **Tier 3** — Payment gateway → idempotency key + pending-first + webhook confirmation.

## 10. Observability Pillars

Full detail: legacy `OBSERVABILITY_STANDARDS.md`. Summary:

- **Logging** — Serilog + `ILogger<T>`, PascalCase structured parameters, no string
  interpolation. Command handlers log `Information` on success, `Warning` on expected
  business failures (before returning `Result.Failure`), `Error` on external failures.
  NEVER log secrets, passwords, tokens, or PII.
- **Tracing** — OpenTelemetry. Custom `ActivitySource` spans for external service calls and
  job execution. `CorrelationId` propagated across the entire request chain.
- **Metrics** — Module-specific `Meter`/`Counter`/`Histogram` via
  `System.Diagnostics.Metrics`.
- **Health checks** — `/health/live`, `/health/ready`, `/health/startup`.

## 11. Infrastructure Rules

### Cache — `ICacheService` only

- Never inject `IDistributedCache`, `IMemoryCache`, or `DaprClient` directly for caching.
- Module key format: `{module}:{entity}:{identifier}` — the infrastructure layer
  automatically prefixes with the tenant ID via `ITenantContextAccessor`. Modules MUST NOT
  hand-roll the tenant prefix.
- Layered: L1 (in-memory, 2 min) → L2 (Redis via Dapr, 15 min) → database.
- Cache-aside: `GetOrSetAsync` for reads, explicit invalidation on writes.

### Background Jobs — `NexoraJob<TParams>`

- All jobs extend `NexoraJob<TParams>` (tenant-aware, logged, traced).
- Naming: `{module}:{action-descriptor}` (e.g. `donations:recurring-charge`).
- Queues: `critical` (payments), `default`, `bulk` (mass ops), `maintenance`.
- Jobs MUST be idempotent — Hangfire retries.
- Max duration 10 minutes; longer work splits into batches.
- Register recurring jobs in `IModule.ConfigureJobs()` using the expression pattern
  `job => job.RunAsync(params, ct)`.

### Secrets — `ISecretProvider` only

- Backed by Dapr Secret Store (Vault in prod).
- Naming: `nexora/{category}/{name}`.
- NEVER put secrets in `appsettings.json`, environment variables, or source.

### Configuration — 5-layer hierarchy

`appsettings.json` → env-specific → env vars → Dapr secrets → tenant DB config.

- `IOptions<T>` / `IOptionsMonitor<T>` for strongly-typed config.
- `ITenantConfiguration` for per-tenant overrides.
- Every config key has a sensible default.
- Validate on startup with `IValidateOptions<T>`.

## 12. Soft Delete

All `AuditableEntity<T>` descendants are soft-deletable:

- `dbContext.Remove(entity)` auto-converts to `IsDeleted = true`.
- Global query filter auto-applies `WHERE IsDeleted = false`.
- `IgnoreQueryFilters()` only in admin/audit views.
- Unique indexes include `HasFilter("\"IsDeleted\" = false")`.
- Hard delete allowed ONLY for: GDPR erasure, join-table reconciliation, uninstall/orphan
  cleanup.
