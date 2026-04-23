# ADR-0024 — SignalR for Events Real-Time Features

**Status:** Accepted  
**Date:** 2026-04-22  
**Revised:** 2026-04-22  
**Deciders:** Platform Team  
**Derives from:** ADR-0016 (Module Tier Classification), ADR-0017 (Portal Extension Architecture)

## Context

The Events-NGO module requires low-latency, push-based updates for two scenarios:
1. **Live check-in dashboard** — event staff need real-time attendee count and check-in status updates during an event (latency target: < 1 second per Events-NGO SPEC NFR §9).
2. **Session capacity indicators** — portal attendees see remaining seats update in near-real-time to prevent overbooking.

Nexora's existing messaging infrastructure (Dapr pub/sub → Kafka) is optimized for reliable, ordered event delivery between backend services. It does not provide a browser-facing WebSocket/long-poll channel. Polling from the frontend is a viable alternative but creates unnecessary load at scale (1,000+ concurrent check-in staff).

Events-NGO SPEC NFRs relevant to this decision:
- Real-time headcount update propagation < 1s
- 200 registrations/second sustained throughput

## Decision

Adopt **ASP.NET Core SignalR** with a **Redis backplane** (direct `StackExchange.Redis` connection) for real-time push to browser clients in the Events-NGO module only.

- The SignalR hub is scoped to the `Nexora.Modules.EventsNGO` module — no shared hub, no global SignalR infrastructure.
- SignalR's backplane bypasses the `ICacheService` abstraction and the Dapr State Store — it connects directly to the same Redis instance that Dapr uses, via `StackExchange.Redis`. This is an explicit, documented exemption to the CLAUDE.md rule "never use `IDistributedCache` / `IMemoryCache` / `DaprClient` directly", because SignalR requires Redis pub/sub primitives (CHANNELs) that the Dapr State Store API does not expose. The connection string is still resolved via `ISecretProvider` (`nexora/redis/connection-string`); only the data-plane access is direct.
- SignalR is NOT used for cross-module communication — that remains Dapr pub/sub (Kafka).
- The hub authenticates clients using the existing APISIX-validated JWT; `TenantId` is extracted from claims.
- Hub groups are keyed by `{tenantId}:{eventId}` to enforce tenant isolation.
- Deployment is cloud-agnostic: the same Redis backplane works in managed cloud, private cloud, and on-prem modes (per ADR-0023). No managed SignalR service is used.

## Alternatives Considered

| Option | Verdict |
|---|---|
| Polling (frontend setTimeout) | Rejected — generates 60+ req/s per 100 users at 1-second intervals; acceptable only for < 50 concurrent users. |
| Server-Sent Events (SSE) | Considered — simpler than WebSocket, but ASP.NET Core SignalR supports SSE as a transport fallback automatically; no net benefit to adopting raw SSE. |
| Dapr pub/sub → frontend via webhook relay | Rejected — adds a relay service, increases latency, no browser WebSocket support. |
| Server-Sent Events via Dapr Binding | Rejected — Dapr output bindings are for service-to-service; no browser-facing binding exists. |
| Azure SignalR Service (managed backplane) | Rejected — creates cloud-vendor lock-in incompatible with on-prem deployment mode (ADR-0023). Nexora must remain cloud-agnostic. |
| SignalR backplane routed through Dapr State Store | Rejected — Dapr State Store API does not expose Redis pub/sub primitives (CHANNELs) required by SignalR. |

## Consequences

**Positive:**
- Sub-1-second push latency to browser clients with no polling overhead.
- Built-in transport negotiation (WebSocket → SSE → long-poll fallback) handles restricted network environments.
- Redis backplane ensures multi-replica deployments work correctly.
- Cloud-agnostic: same topology in managed, private, and on-prem deployments.

**Negative / Risks:**
- Adds a new infrastructure dependency (SignalR hub) with sticky-session or backplane requirements.
- Redis backplane adds latency (~5 ms) vs. in-process; acceptable for this use case.
- SignalR connections are stateful — hub must handle disconnection and reconnection gracefully.
- Documented exemption from the `ICacheService` / `DaprClient` rule for backplane access only.
- Limited to Events-NGO; other modules requesting real-time features should open a new ADR.

## Implementation Notes

- Hub class: `EventsHub : Hub` in `Nexora.Modules.EventsNGO.Api.Hubs`.
- Registration: `services.AddSignalR().AddStackExchangeRedis(connectionString)` in `EventsNGOModule.ConfigureServices()`.
- Hub endpoint: `/hubs/events` (mounted by the module, not the host).
- Tenant isolation: `Context.User` must carry `tenant_id` claim; `OnConnectedAsync` validates and adds the client to group `{tenantId}:{eventId}`.
- Connection string: `ISecretProvider.GetSecretAsync("nexora/redis/connection-string", ct)`.

### Authentication lifecycle

- WebSocket upgrades carry the JWT once in the initial HTTP request; subsequent frames are NOT re-validated by the transport.
- The hub enforces authentication via `JwtBearerOptions.Events.OnMessageReceived`, which reads `access_token` from the query string when the request path matches `/hubs/events` (SignalR convention — browsers cannot set `Authorization` headers on WebSocket upgrades).
- When the JWT expires during an active connection, the hub sends a `ForceReconnect` message; the client disconnects and reconnects with a fresh token.
- Clients use SignalR's `accessTokenFactory` pattern — the factory returns the current token from the existing auth store and is invoked on every (re)connect.
- If the factory returns a token in the `RefreshAccessTokenError` state (per API_INTEGRATION_STANDARDS §4 auth gate: `!token || token.error === 'RefreshAccessTokenError'`), the connection is not retried and the user is redirected to login.

### Disconnection and missed-event replay

- The hub does NOT guarantee at-least-once delivery. On reconnect, the client requests a state sync via a regular HTTP query (`GET /api/v1/events/{id}/checkin-state?since={lastSeenTs}`) to fetch missed updates.
- The client tracks `lastSeenTimestamp` per subscription and sends it as a query parameter on reconnect-triggered syncs.
- For check-in dashboards: missed check-ins during disconnect are recovered by the sync call, not by the hub. This is the authoritative path for correctness; SignalR is a latency optimization only.
- Kafka integration events (`event.attendee.checked-in`) remain the source of truth for server-side side effects; the hub merely relays notifications to connected clients.

### Deployment requirements

- Kubernetes ingress must enable session affinity: `service.spec.sessionAffinity: ClientIP`, or the ingress annotation `nginx.ingress.kubernetes.io/affinity: "cookie"`.
- APISIX: enable the `consistent-hash` or `sticky-session` plugin on the SignalR upstream route. Route key: `/hubs/events/*`.
- WebSocket upgrades must be allowed end-to-end (APISIX → K8s ingress → pod). The APISIX route requires `enable_websocket: true`.
- With the Redis backplane active, sticky sessions are NOT strictly required for correctness (the backplane handles cross-pod broadcast), but ARE required during transport fallback to long-polling (where per-request state must hit the same pod).

### Frontend integration

- SignalR messages are treated as **invalidation signals** for TanStack Query, not as direct state writes.
- Flow: hub push received → call `queryClient.invalidateQueries([...])` for affected keys → TanStack Query refetches from the HTTP API (which is the authoritative source).
- For high-frequency updates (live attendee count): use optimistic cache patching (`queryClient.setQueryData`) to avoid refetch storms; debounce invalidations at 500 ms.
- Presence / transient UI state (connected/disconnected badge) lives in **Zustand** (`eventsRealtimeStore`), not TanStack Query.
- Client library: `@microsoft/signalr@^8.x` — compatible with React 19 (no peer dep on React). Used in both `nexora-admin` and `nexora-portal`.

### Localization of pushed messages

SignalR-pushed messages MUST follow LOCALIZATION_STANDARDS — payloads carry `lockey_` keys (e.g. `lockey_events_attendee_checked_in`) and resource identifiers; the frontend resolves translations via `react-i18next` (admin) / `next-intl` (portal). The hub NEVER pushes pre-translated strings.

### Scale and throughput limits

- Target: 1,000 concurrent connections per hub replica.
- Scale-out: horizontal pod autoscaler triggers at 700 connections/pod (70% of target).
- Redis backplane throughput budget: 5,000 messages/sec/tenant (soft limit; monitor and alert).
- Broadcast fan-out limit: group size capped at 5,000 subscribers; above this, shard the event into multiple sub-groups.
- Aligned with Events-NGO SPEC NFR: propagation < 1 s at 200 registrations/sec sustained.

## Observability

- **Metrics** (via `Meter` named `Nexora.Modules.EventsNGO.Hubs`):
  - `events_signalr_connections_active` (ObservableGauge) — current hub connections per tenant.
  - `events_signalr_messages_sent_total` (Counter) — messages broadcast, labelled by event-type.
  - `events_signalr_connection_duration_seconds` (Histogram).
  - `events_signalr_reconnects_total` (Counter).
- **Tracing**: custom `ActivitySource("Nexora.Modules.EventsNGO.Hubs")` wraps `OnConnectedAsync`, `OnDisconnectedAsync`, and broadcast methods; span attributes include `tenant.id` and `event.id`.
- **Health checks**: `/health/ready` includes a Redis backplane probe — fails if Redis is unreachable. `/health/live` remains process-only; `/health/startup` covers hub registration.
- **Structured logs**: `Information` on connect/disconnect with `TenantId` + `ConnectionId`; `Warning` on auth failure and backplane errors. No secrets, tokens, or PII are logged.

## Testing

- **Integration tests** use `WebApplicationFactory<T>` + `HubConnectionBuilder` pointed at the test server; no real Redis required (in-memory backplane for tests via `AddSignalR()` without `AddStackExchangeRedis`).
- **Contract tests**: broadcast messages are validated against a schema (Fluent assertions on the message envelope — message name, lockey payload shape, required identifiers).
- **Latency NFR test** (`< 1 s propagation`): load-test scenario using `NBomber` or `k6` with 1,000 concurrent WebSocket clients; measured in staging only, not in the PR gate.
- **Architecture tests**: assert that the backplane exemption is confined to `Nexora.Modules.EventsNGO` — no other module references `StackExchange.Redis` directly.

## ADR Ledger

- **Introduces**: SignalR infrastructure for Events-NGO; documented exemption to `ICacheService`-only rule for SignalR backplane.
- **Consumes**: ADR-0002 (Multi-Tenancy), ADR-0013 (Cache Cross-Instance Invalidation — same Redis instance), ADR-0023 (Deployment modes including on-prem).
- **Related**: ADR-0016 (Module Tier Classification), ADR-0017 (Portal Extension Architecture).
