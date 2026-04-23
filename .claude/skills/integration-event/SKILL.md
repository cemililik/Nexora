---
name: integration-event
description: Publish or consume a cross-module integration event via Dapr pub/sub (Kafka) or MediatR notification. Use when the user asks to "publish event", "subscribe to X event", or needs cross-module communication without breaking module boundaries.
---

# Integration Event

Modules **MUST NOT** reference each other's internal types. Cross-module communication goes through:
1. **MediatR notifications** — in-process, same host, synchronous-ish.
2. **Dapr pub/sub (Kafka)** — out-of-process, durable, async. Preferred for anything that can tolerate eventual consistency.
3. **SharedKernel interfaces** — for read-only queries across module boundaries.

## Decide first
| Need | Use |
|------|-----|
| Same-process fan-out, low latency, small payload | MediatR `INotification` |
| Durable delivery, retry, out-of-process consumers | Dapr pub/sub |
| Read a value owned by another module | `Nexora.SharedKernel` interface |

## Event payload rules
- Define the contract in `Nexora.SharedKernel/IntegrationEvents/` (so publisher and consumer both reference it).
- Use strongly-typed IDs — never raw `Guid`.
- Include `TenantId`, `OccurredAt` (UTC), `EventId` (for dedupe).
- Records only, immutable: `public sealed record ContactCreatedIntegrationEvent(...)`.
- Name: `{Entity}{Verb-Past}IntegrationEvent` (e.g., `DonationCapturedIntegrationEvent`).

## Publishing (Dapr pub/sub)
- Topic name: `nexora.{module}.{entity}.{verb}` (e.g., `nexora.donations.donation.captured`).
- Publish from the command handler **after** the DB transaction commits — use outbox pattern if available.
- Log `Information` with `{EventId}` and `{TenantId}`.

## Consuming
- Handler in the consuming module's `Infrastructure/IntegrationEventHandlers/`.
- **Idempotent**: dedupe by `EventId` (store processed IDs per-tenant).
- `Result`-based; on failure return failure (Dapr will retry per configured policy).
- Log Warning on business-rule rejection, Error on external failure, Information on success.
- No `catch(Exception)` — let the pipeline handle.

## MediatR notification variant
- Define: `public sealed record ContactCreatedNotification(...) : INotification;`
- Publish from handler: `await mediator.Publish(notification, ct);`
- Consume: `INotificationHandler<ContactCreatedNotification>` — same idempotency / logging rules apply.

## Testing
- Contract tests in `tests/` verify the integration event record is in SharedKernel and unchanged (breaking-change guard).
- Handler tests assert idempotency (same `EventId` twice → no double effect).
