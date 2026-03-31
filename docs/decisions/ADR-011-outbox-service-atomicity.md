# ADR-011: Outbox Service Atomicity Fix

## Status

Accepted

## Date

2026-03-31

## Context

The initial implementation of the transactional outbox pattern (documented in [ADR-005](./ADR-005-transactional-outbox-pattern.md)) used a non-generic `OutboxService` that called `SaveChangesAsync` internally within `EnqueueAsync`. This created a **separate transaction** for the outbox record, breaking the atomicity guarantee described in ADR-005. If the caller's transaction rolled back after `EnqueueAsync` succeeded, the outbox message would still be persisted and eventually published as a phantom event.

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

- Supersedes implementation details of [ADR-005: Transactional Outbox Pattern](./ADR-005-transactional-outbox-pattern.md)
