# ADR-014: Distributed Consistency Patterns

## Status
Accepted

## Date
2026-04-01

## Context

Nexora is a modular monolith that calls external services within command handlers — currently
Keycloak (identity), MinIO (object storage), and shortly Stripe/iyzico (payments). Several
incidents revealed that multi-step operations can leave the system in an inconsistent state
when a step fails:

- `CreateUserCommand`: Keycloak user created → DB insert fails → orphaned Keycloak user
- `DeleteUserCommand`: DB soft-delete → filtered index missing → 23505 on re-registration
- `CreateTenantCommand`: Tenant persisted → Keycloak realm creation fails → tenant has no realm
- `UpdateUserStatusCommand`: Keycloak called before DB write → Keycloak fails → DB never updated

A codebase-wide audit identified the following consistency categories:

| Category | Count | Risk |
|---|---|---|
| Single DB transaction (no external calls) | Most handlers | Low — already correct |
| DB + Keycloak (IAM sync) | 4 handlers | Medium–High — no compensation |
| DB + MinIO (object storage) | Read-only checks | Low — idempotent |
| DB + Hangfire enqueue | 1 handler | Medium — two SaveChanges splits atomicity |
| DB + Payment gateway (planned) | Phase 2.3, 4.4 | Critical — financial data |
| Cross-module long-running (planned) | Phase 4+ | Critical — saga required |

## Decision

We adopt a **four-tier consistency model** described in full in
`docs/standards/CONSISTENCY_STANDARDS.md`. The tiers are:

**Tier 1 — Single DB Transaction**
Standard EF Core usage. No new rules.

**Tier 2A — DB-First**
When the external service is a mirror of our DB state (Keycloak enable/disable, status syncs):
write to DB first, call external service after. External call failure is non-fatal — log Warning,
continue. A future reconciliation job will repair divergence.

**Tier 2B — External-First + Compensation**
When the external service returns data the DB record needs (Keycloak `CreateUser` returns
`keycloakUserId`): call external service first, write to DB, and **compensate** (undo the
external call) if the DB write fails. If compensation also fails, emit `LogCritical` for
manual intervention.

**Tier 3 — Payment Gateway Pattern**
For any financial transaction: idempotency key + pending-first DB write + webhook confirmation
+ nightly reconciliation job. Required before shipping any payment feature.

**Tier 4 — Saga Pattern**
Deferred to Phase 4. Required when an operation spans 3+ external systems with long-running
steps. The existing Outbox/Inbox infrastructure serves as the transport layer.

### Why Not Full Saga Now?

- Only 4 handlers require consistency fixes, all within one module (Identity).
- The problematic external system (Keycloak) is simple enough for compensating transactions.
- Saga infrastructure (state machine, coordinator, compensation registry) would take 4–6 weeks
  and is not justified until Phase 4 multi-module pipelines exist.
- Overbuilding now would constrain future architectural decisions about the saga coordinator.

### Why Not Outbox-Deferred Keycloak Calls?

Deferring Keycloak provisioning to the Outbox introduces a "Pending" user state that propagates
through the entire system (UI must handle it, API must handle it, tests must handle it). For
Tier 2B operations like `CreateUserCommand`, the synchronous compensation is simpler and the
failure scenario (Keycloak success + DB failure) is rare and recoverable.

For Tier 2A operations (status syncs), DB-first is already the correct semantic. The Outbox
would add latency without benefit.

## Consequences

### Positive
- Eliminates orphaned Keycloak records on user/tenant creation failures.
- DB is always the authoritative source of truth for user and tenant state.
- Payment integrity is guaranteed before any financial module ships.
- The pattern is explicit and reviewable: code reviewers can check tier compliance.
- No new infrastructure required for Tier 1–2; existing Inbox guard used for Tier 3.

### Negative
- Tier 2B compensation adds try/catch nesting in command handlers — accepted complexity.
- Tier 2A failures leave Keycloak temporarily out of sync — requires eventual reconciliation
  (planned as a maintenance job in Phase 2).
- Tier 3 requires significant upfront work before each payment gateway integration.

### Risks
- Compensation calls can fail (Keycloak is down when we try to delete the orphaned user).
  Mitigation: `LogCritical` creates an alert; manual cleanup is the fallback.
- Payment reconciliation job must ship with (not after) the first payment feature.
- Saga deferral means Phase 4 enrollment pipeline will need a dedicated architecture review
  before implementation.

## Alternatives Considered

| Alternative | Pros | Cons | Why Rejected |
|---|---|---|---|
| **Full Saga pattern now** | Handles all scenarios uniformly | 4–6 weeks of infrastructure; premature for 4 handlers in one module | Not justified at current scale |
| **Outbox-deferred Keycloak** | DB always consistent; Keycloak catches up | "Pending" user state ripples into UI, API, tests; adds latency | Complexity cost exceeds benefit for synchronous provisioning |
| **Two-Phase Commit (XA)** | Strongest guarantee | Keycloak does not support XA; PostgreSQL XA has performance cost | Not feasible with Keycloak |
| **Accept inconsistency + monitoring** | Zero development cost | Financial data divergence is not tolerable; IAM orphans cause login failures | Unacceptable risk profile |
