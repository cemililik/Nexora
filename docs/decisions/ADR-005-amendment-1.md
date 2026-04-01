# ADR-005 Amendment 1: Per-Message Isolated Transaction in OutboxProcessor

## Amends

[ADR-005: Transactional Outbox Pattern](./ADR-005-transactional-outbox-pattern.md)

## Status

Accepted

## Date

2026-03-31

## Context

The original `OutboxProcessor` implementation (ADR-005) used a **single shared transaction** across all messages in a polling batch. Under this design:

1. The `SELECT … FOR UPDATE SKIP LOCKED` and all subsequent `UPDATE` statements (mark-processed or record-failure) shared one `NpgsqlTransaction`.
2. A single message failure that caused an exception before `tx.CommitAsync()` would roll back **all row-lock updates in the batch**, leaving every message in an indeterminate state until the next polling cycle.
3. `CancellationToken` was passed to `tx.CommitAsync()`, meaning a shutdown signal during commit could leave a message published-but-not-marked-processed (ghost event) on the next restart.

## Decision

`OutboxProcessor` is refactored to use **per-message isolated transactions**:

1. **Batch fetch**: A short-lived transaction executes `SELECT … FOR UPDATE SKIP LOCKED` to claim a batch of message IDs. It commits immediately with `CancellationToken.None` (so the lock release is never interrupted).
2. **Per-message processing**: Each message opens its own `NpgsqlConnection` and `NpgsqlTransaction`. The transaction commits with `CancellationToken.None` after the bus publish and the `UPDATE ProcessedAt` succeed, ensuring partial-batch failures are isolated.
3. **Failure isolation**: If one message's bus publish throws, only that message's transaction is affected. Remaining messages in the batch are processed in subsequent iterations of the `foreach` loop.

### Implementation Reference

See [`src/Nexora.Infrastructure/Persistence/Outbox/OutboxProcessor.cs`](../../src/Nexora.Infrastructure/Persistence/Outbox/OutboxProcessor.cs) — methods `FetchPendingMessagesAsync` and `ProcessMessageAsync`.

## Consequences

- **Positive**: A single failing message no longer blocks an entire batch. System throughput degrades gracefully under partial broker failure.
- **Positive**: Commit is never interrupted by `CancellationToken`, eliminating the published-but-not-marked ghost-event risk.
- **Negative**: Each message now opens its own `NpgsqlConnection`, increasing connection overhead for large batches. Mitigated by the Npgsql connection pool.
- **Neutral**: The `FOR UPDATE SKIP LOCKED` pattern is preserved; concurrent processor instances (e.g., multiple replicas) still coordinate correctly.
