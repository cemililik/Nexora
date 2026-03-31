# ADR-011 Addendum 1: OutboxOptions Startup Validation and Connection String Configuration

## Amends

[ADR-011: Outbox Service Atomicity Fix](./ADR-011-outbox-service-atomicity.md)

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
