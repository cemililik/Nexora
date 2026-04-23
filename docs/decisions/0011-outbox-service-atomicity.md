# ADR-011: Outbox Service Atomicity Fix

## Status

Accepted

## Date

2026-03-31

## Context

The initial implementation of the transactional outbox pattern (documented in [ADR-005](./0005-transactional-outbox.md)) used a non-generic `OutboxService` that called `SaveChangesAsync` internally within `EnqueueAsync`. This created a **separate transaction** for the outbox record, breaking the atomicity guarantee described in ADR-005. If the caller's transaction rolled back after `EnqueueAsync` succeeded, the outbox message would still be persisted and eventually published as a phantom event.

## Decision

We introduced a generic `OutboxService<TContext>` and `InboxGuard<TContext>` to restore true atomicity, along with processor improvements.

### Fix

`OutboxService<TContext>` is now generic, parameterized by the caller's module-specific `DbContext`. `EnqueueAsync` adds the `OutboxMessage` to the `DbContext` change tracker but does **not** call `SaveChangesAsync`. The outbox record is committed only when the caller's command handler calls `SaveChangesAsync`, ensuring true single-transaction atomicity.

### Additional improvements

- `OutboxProcessor` iterates tenant schemas (multi-tenant aware).
- Reflection caching for event type resolution (performance).
- Immediate retry when a full batch is processed (reduced latency).
- `InboxGuard<TContext>` follows the same generic per-module `DbContext` pattern.

## Consequences

### Positive

- **Atomicity restored**: Outbox record and business data are committed in a single transaction — no phantom events on caller rollback.
- **Generic `DbContext` pattern**: `OutboxService<TContext>` and `InboxGuard<TContext>` each bind to the caller's module `DbContext`, keeping module boundaries intact.
- **Multi-tenant iteration**: `OutboxProcessor` iterates all active tenant schemas, so a single processor instance handles the entire fleet.
- **Reflection caching**: `PublishMethodCache` avoids repeated `MakeGenericMethod` allocations per message type.
- **Immediate retry on full batch**: When a full batch is processed, the processor polls again immediately rather than waiting for the next interval, reducing end-to-end latency under load.

### Negative

- **Generic type complexity**: Every module that enqueues outbox messages must pass its `DbContext` type parameter to `OutboxService<TContext>`, increasing registration boilerplate.
- **Migration burden**: Module DbContexts must include the `OutboxMessage` configuration (`OutboxMessageConfiguration`). Adding a new module requires applying this configuration explicitly.
- **Schema iteration overhead**: The processor queries `IActiveTenantProvider` on every poll cycle. Under high tenant counts this adds latency to each poll interval.

## Related

- Supersedes implementation details of [ADR-005: Transactional Outbox Pattern](./0005-transactional-outbox.md)


---

## Addendum 1 — OutboxOptions Startup Validation and Connection String Configuration

## Amends

[ADR-011: Outbox Service Atomicity Fix](./0011-outbox-service-atomicity.md)

## Status

Accepted

## Date

2026-03-31

## Context

ADR-011 introduced `OutboxProcessor` and `OutboxOptions` but left two gaps:

1. **Raw `IConfiguration` access**: `OutboxProcessor.ProcessAllTenantsAsync` resolved the database connection string by calling `IConfiguration.GetConnectionString("Default")` at runtime. This violates the project standard that all configuration must flow through strongly-typed `IOptions<T>` classes validated at startup (see CLAUDE.md § Configuration).

2. **Missing startup validation**: `OutboxOptions` had no `IValidateOptions<OutboxOptions>` implementation, meaning misconfigured values (e.g., `BatchSize = 0`, missing connection string) would only surface as runtime failures — potentially hours into production operation.

## Decision

### 1. ConnectionString moved into OutboxOptions

A `ConnectionString` property is added to `OutboxOptions`. The outbox processor reads it from `_options.ConnectionString` instead of from `IConfiguration`. The connection string is bound in the same configuration section (`Outbox`) as all other outbox settings.

```json
{
  "Outbox": {
    "ConnectionString": "...",
    "PollingIntervalSeconds": 5,
    "BatchSize": 100
  }
}
```

`OutboxProcessor` no longer takes `IConfiguration` as a dependency.

### 2. OutboxOptionsValidator added

`OutboxOptionsValidator : IValidateOptions<OutboxOptions>` is registered with the DI container and validates all options on startup:

- `PollingIntervalSeconds`, `BatchSize`, `MaxRetryCount`, `CleanupAfterDays`, `DegradedThreshold` must each be `> 0`.
- `UnhealthyThreshold` must be strictly greater than `DegradedThreshold`.
- `ConnectionString` must not be null or whitespace.

A misconfigured outbox will prevent the host from starting (fail-fast) rather than silently misbehaving at runtime.

### Implementation Reference

See [`src/Nexora.Infrastructure/Persistence/Outbox/OutboxOptions.cs`](../../src/Nexora.Infrastructure/Persistence/Outbox/OutboxOptions.cs) for the validator and the new property.

## Consequences

- **Positive**: Configuration errors are caught at startup, not at first polling cycle.
- **Positive**: `OutboxProcessor` no longer depends on `IConfiguration`, aligning with the strongly-typed configuration standard.
- **Negative**: The connection string now appears twice in `appsettings.json` (under `ConnectionStrings:Default` and `Outbox:ConnectionString`). Teams must keep both in sync. A future improvement could bind `OutboxOptions.ConnectionString` directly from `ConnectionStrings:Default` in the registration helper.
