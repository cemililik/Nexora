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

## Related
- Supersedes implementation details of [ADR-005: Transactional Outbox Pattern](./ADR-005-transactional-outbox-pattern.md)
