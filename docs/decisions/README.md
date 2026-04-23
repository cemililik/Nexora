# Architecture Decision Records (ADRs)

This directory holds every architecture decision that shapes Nexora. ADRs are the authoritative
record of *why* the system is the way it is. Code and specs explain *what* and *how*; ADRs
explain *why*.

## Numbering

- **Format**: `NNNN-kebab-title.md` (four-digit, zero-padded, sequential).
- Numbers are **never reused**. Once an ADR gets a number, that number is permanent, even if the
  ADR is later superseded or obsoleted.
- Pick the next free number by taking the highest existing `NNNN` and adding one.

## Status model

Each ADR carries one status at a time:

| Status | Meaning |
|--------|---------|
| `Proposed` | Under discussion. Open to feedback; decisions and wording may change. |
| `Accepted` | Agreed and in force. The decision drives code, docs, and reviews. |
| `Superseded` | Replaced by a newer ADR. The superseding ADR's number is noted in a `Superseded by` line. |

Status transitions are forward-only: `Proposed → Accepted → Superseded`. An ADR can also go
`Proposed → Rejected` (rare; leave the file in place for history).

## Immutability rule

Accepted ADRs are **not edited in place**. If a decision needs to change:

1. Write a new ADR that supersedes the old one. The new ADR links back; the old ADR gets a
   `Status: Superseded by NNNN` line added at the top (this is the only permitted edit).
2. For small clarifications that do not change the decision, append an
   `## Amendment N — <title>` or `## Addendum N — <title>` section at the bottom with its own
   date. Amendments must not contradict the original decision.

This keeps the historical reasoning intact and makes "why did we decide X at time T?"
answerable from the Git log alone.

## Writing a new ADR

1. Copy [`template.md`](./template.md) to `NNNN-your-title.md` with the next free number.
2. Fill in every section; omit sections only if they genuinely do not apply.
3. Open with status `Proposed`. Promote to `Accepted` only after review.
4. Prefer the [`write-adr` skill](../../.claude/skills/write-adr/) (when available) to scaffold
   the file with the correct front matter and references.

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](./0001-modular-monolith.md) | Modular Monolith Architecture | Accepted |
| [0002](./0002-schema-per-tenant.md) | Schema-per-Tenant Multi-Tenancy | Accepted |
| [0003](./0003-deployment-strategy.md) | Deployment Strategy | Accepted |
| [0004](./0004-centralized-permission-seeding.md) | Centralized Permission Seeding | Accepted |
| [0005](./0005-transactional-outbox.md) | Transactional Outbox Pattern (+ Amendment 1) | Accepted |
| [0006](./0006-permission-based-authorization.md) | Permission-Based Authorization | Accepted |
| [0007](./0007-tab-based-layout.md) | UX/UI Tab-Based Layout Standard | Accepted |
| [0008](./0008-gdpr-deletion-strategy.md) | GDPR Deletion Strategy | Accepted |
| [0009](./0009-audit-repository-pattern.md) | Audit Module Repository Pattern | Accepted |
| [0010](./0010-notification-delivery-kafka.md) | Notification Delivery via Kafka | Accepted |
| [0011](./0011-outbox-service-atomicity.md) | Outbox Service Atomicity Fix (+ Addendum 1) | Accepted |
| [0012](./0012-tenant-management.md) | Tenant Management & Authorization | Accepted |
| [0013](./0013-cache-cross-instance-invalidation.md) | Cache Cross-Instance Invalidation | Accepted |
| [0014](./0014-distributed-consistency-patterns.md) | Distributed Consistency Patterns | Accepted |
| [0015](./0015-roadmap-structure.md) | Roadmap Structure and Phase Model | Accepted |
| [0016](./0016-module-tier-classification.md) | Module Tier Classification | Accepted |
| [0017](./0017-portal-extension-architecture.md) | Portal Extension Architecture | Accepted |
| [0024](./0024-signalr-for-events-realtime.md) | SignalR for Events Real-Time Features | Accepted |
| [0025](./0025-org-scoped-compliance-config-with-platform-caps.md) | Org-Scoped Compliance Config with Platform-Level Caps | Accepted |
| [0026](./0026-cross-module-pii-payload-scan-for-gdpr-erasure.md) | Cross-Module PII Payload Scan for GDPR Erasure | Proposed |
| [0027](./0027-production-schema-migration-strategy.md) | Production Schema-Migration Strategy | Proposed |
| [0028](./0028-module-uninstall-data-retention-contract.md) | Module Uninstall Data-Retention Contract | Proposed |
