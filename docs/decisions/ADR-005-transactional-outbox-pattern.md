# ADR-005: Transactional Outbox Pattern

## Status
Accepted

## Date
2026-03-31

## Context

Domain events published directly to Kafka could be lost if Kafka is unavailable or the application crashes between the database commit and the Kafka publish call. This creates a reliability gap: the domain state changes but downstream consumers never receive the event.

Two approaches were considered:

1. **Approach A — Atomic outbox (CDC-based)**: Use a Change Data Capture tool (e.g., Debezium) to tail the outbox table's WAL and publish to Kafka. Fully atomic but adds operational complexity.
2. **Approach B — Polling outbox**: Handlers write events to a local PostgreSQL outbox table instead of publishing to Kafka directly. A background service polls and publishes.

## Decision

We will use **Approach B — Polling outbox**.

When a domain event is raised, the handler writes a serialized event row to the `outbox_messages` table within the same database transaction as the domain state change. An `OutboxProcessor` (`BackgroundService`) polls the outbox table on a configurable interval and publishes pending messages to Kafka via Dapr pub/sub. Successfully published messages are marked as processed.

This is not fully atomic (a crash between Kafka publish and marking the row as processed could cause a duplicate), but consumers are expected to be idempotent. The key benefit is eliminating the external service dependency during the write path.

### How It Works

```mermaid
sequenceDiagram
    participant H as Command Handler
    participant DB as PostgreSQL
    participant OP as OutboxProcessor
    participant K as Kafka (Dapr)

    H->>DB: BEGIN transaction
    H->>DB: Save domain entity
    H->>DB: Insert outbox_messages row
    H->>DB: COMMIT
    OP->>DB: Poll for unprocessed messages
    DB-->>OP: Return pending messages
    OP->>K: Publish to Kafka topic
    K-->>OP: Ack
    OP->>DB: Mark message as processed
```

## Consequences

### Positive
- **Reliable delivery**: Events are never lost on Kafka outages — they remain in the outbox table until successfully published
- **No external dependency on write path**: The command handler only writes to PostgreSQL, which is always available if the domain write succeeds
- **Uses existing infrastructure**: No additional tools like Debezium required
- **Simple to reason about**: The outbox table is queryable and auditable

### Negative
- **Added latency**: Up to ~5 seconds max event latency due to polling interval
- **Extra DB write**: +1 INSERT per domain event (outbox row)
- **At-least-once delivery**: Consumers must be idempotent to handle potential duplicates

### Risks
- **Outbox table growth**: If Kafka is down for extended periods, the outbox table grows. Mitigation: alerting on unprocessed message count, TTL-based cleanup of processed rows.

## Alternatives Considered

| Alternative | Pros | Cons | Why Rejected |
|------------|------|------|-------------|
| Direct Kafka publish after DB commit | Simplest code path | Event loss on Kafka outage or app crash | Unacceptable reliability gap |
| CDC-based outbox (Debezium) | Fully atomic, lower latency | Operational complexity, WAL access required, additional infrastructure | Over-engineering for current scale |

## Update (2026-03-31): Atomicity Fix

For details on the subsequent fix that resolves atomicity issues and introduces generic `OutboxService<TContext>`, refer to [ADR-011: Outbox Service Atomicity Fix](./ADR-011-outbox-service-atomicity.md).

## Related
- [ADR-010: Notification Delivery via Kafka](./ADR-010-notification-delivery-kafka.md)
- Outbox implementation: `src/Nexora.Infrastructure/Persistence/Outbox/OutboxProcessor.cs`
