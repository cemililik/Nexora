# 0031 — Cascade uninstall uses per-module transactions with forward-log compensation

## Status

Accepted

## Date

2026-04-24

## Supersedes (in part)

- [ADR-0028](0028-module-uninstall-data-retention-contract.md) — Module Uninstall Data-Retention Contract.
  ADR-0028 remains in force for **everything except** its cascade transaction
  mechanism (the *"per-module savepoints inside a single outer transaction
  guarded by `pg_advisory_xact_lock(hashtext('uninstall:'||tenant_id))`"*
  paragraph in §Decision outcome). That mechanism is replaced by this ADR's
  per-module-transaction + forward-log + compensation model. The retention
  window, cleanup job, GDPR escape hatch, dependency guard, extended event
  schema, and all other ADR-0028 commitments stand unchanged.

## Context

ADR-0028 (Accepted 2026-04-23) formalised the module-uninstall contract.
Implementation work began on the three follow-up tasks (T-025 cleanup
job, T-026 cascade guard, T-027 GDPR escape hatch). T-026's acceptance
criteria collided with ADR-0028's stated cascade transaction mechanism:

> ADR-0028 §Decision outcome (paraphrased): the cascade orchestrator opens a
> single outer transaction, holds `pg_advisory_xact_lock(hashtext('uninstall:'||tenant_id))`
> for its duration, and runs each module's `OnUninstallAsync` + table rename +
> TenantModule soft-delete inside its own savepoint. A failure at module N
> rolls back to that savepoint; earlier modules in the same outer transaction
> remain uninstalled.

This mechanism is **not implementable** in the current Nexora architecture:

1. **Per-module DbContext isolation.** Every Nexora module owns its own
   `DbContext` instance ([`docs/architecture/MODULE_SYSTEM.md`](../architecture/MODULE_SYSTEM.md) §Module Boundaries:
   *"Each module has its OWN DbContext — no shared tables across module boundaries"*).
   A "single outer transaction" spanning N modules' work requires either
   (a) sharing one `DbConnection` across all DbContexts — which breaks
   `BaseDbContext.HasDefaultSchema(tenant_schema)` because schema
   resolution is per-DbContext-instance and a shared connection's
   `search_path` cannot serve N different schema cache keys safely; or
   (b) promoting the cascade to a distributed-transaction coordinator,
   which [ADR-0001](0001-modular-monolith.md) explicitly rejects as a
   platform primitive (Modular Monolith over DTC; coordinated state
   moves through outbox/inbox, not 2PC).

2. **`pg_advisory_xact_lock` lifetime mismatch.** The advisory lock is
   tied to the *transaction* lifetime. With per-module transactions the
   lock would release between modules — the very property ADR-0028 was
   trying to enforce. A session-level lock (`pg_advisory_lock`,
   non-`xact`) is the correct primitive for a coordinator that spans
   multiple transactions.

3. **DDL inside long outer transactions on Tier-2 multi-table modules.**
   ADR-0028 itself flagged the operational caveat:
   *"the outer transaction holds DDL locks on every renamed table for its full duration,
   which can stall long-running reads on those tables and increase replication lag."*
   The mitigation it proposed ("operators MUST prefer per-module compensation
   over a single atomic cascade once the chain length exceeds 3 or any
   module has > 50M rows") effectively concedes the outer-transaction
   model is wrong for production-scale tenants — a contract that
   discourages its own happy path is a contract that wants amending.

T-026's status log captured the right answer in passing; this ADR makes
it the normative cascade transaction mechanism so the implementer is not
forced to choose between contradicting ADR-0028 and shipping a
non-implementable design.

## Decision drivers

- **Module-DbContext boundary holds.** No shared connections, no DTC,
  no shared transaction across modules. ADR-0001 + MODULE_SYSTEM.md are
  load-bearing constraints, not nice-to-haves.
- **Mutual-exclusion still required.** Two operators must not be able
  to launch concurrent cascades against the same tenant; the rename
  pattern is not safe under that race.
- **Atomic-per-module is sufficient.** Each module's uninstall step
  (OnUninstallAsync → rename → TenantModule.IsDeleted=true → outbox
  enqueue) MUST be atomic *within* that module. Cross-module atomicity
  is replaced by **explicit, observable compensation** the operator can
  reason about.
- **Failures are reportable, not invisible.** A partial-failure cascade
  must surface the exact set of modules that uninstalled and the exact
  set that did not — operators choose whether to retry, escalate, or
  accept the partial state.
- **Operational predictability.** Long DDL holds across N modules are
  the production failure mode ADR-0028 itself warned about; this
  decision must not regress that.

## Considered options

### 1. Per-module transactions + session advisory lock + forward log + compensation *(chosen)*

Cascade orchestrator pseudo-code:

```text
acquire pg_advisory_lock(hashtext('uninstall:' || tenant_id))   -- session-scoped
try:
    successes = []
    for module in modules_in_reverse_dependency_order:
        begin transaction (on module's own DbContext)
        try:
            OnUninstallAsync(module)
            rename module's tables to {table}_del_{ts}
            TenantModule.IsDeleted = true; record DeletedTableNames
            outbox.Enqueue(ModuleUninstalledIntegrationEvent { CanonicalTableNames, RenamedTableNames, UninstalledAtUtc })
            commit
            successes.append(module)
        except:
            rollback
            outbox.Enqueue(ModuleUninstallFailedIntegrationEvent { module, canonicals_seen_so_far })
            raise CascadePartialFailure(succeeded = successes, failed = module)
finally:
    release pg_advisory_lock
```

Compensation contract:

- On `CascadePartialFailure`, the orchestrator does NOT auto-undo the
  successes. The successful modules' rename-to-`_del_` is reversible
  by ADR-0028's reinstall-within-retention path — that **is** the
  compensation primitive. The operator sees the partial-failure event,
  decides whether to:
  1. fix the failing module's underlying cause and re-issue the cascade
     (the already-uninstalled modules are no-ops because their
     `TenantModule.IsDeleted` is already true and their tables already
     renamed); or
  2. reinstall the affected modules within the retention window if they
     prefer to back out.
- Modules MAY override `OnUninstallAsync` to publish their own
  domain-specific compensating actions (e.g. canceling outbound
  webhooks, releasing reserved external resources). The default no-op
  is acceptable for modules whose uninstall is trivially reversible
  via the rename pattern.
- Non-trivially-irreversible operations (e.g. a module that physically
  drops external state on uninstall before the rename) MUST flag
  themselves in their `OnUninstallAsync` documentation; the cascade
  coordinator does NOT special-case them but the module owner is on
  the hook for documenting recovery.

- **Pros**
  - Compatible with the per-module DbContext boundary; no shared
    connection, no DTC.
  - Session advisory lock matches the cross-transaction lifetime the
    coordinator actually needs.
  - DDL locks held only for the duration of one module's transaction
    — eliminates the long-DDL-hold failure mode ADR-0028 warned about.
  - Compensation primitive (rename-back inside retention) already
    exists — no new infrastructure to build.
  - Partial failure is *observable* (event + structured log + audit row);
    operators can act on it.
- **Cons**
  - "Cascade" no longer means "all-or-nothing" — operators must
    learn that a cascade can land in a partial-success state by design.
  - The compensation event (`ModuleUninstallFailedIntegrationEvent`)
    must be wired through outbox → operator-visible surface (admin UI
    notification + structured log alert). Documented as a follow-up
    inside the T-026 spec.

### 2. Outbox-driven saga (Sagas as orchestrator)

Replace the synchronous cascade with a saga: each step posts an event
the next step consumes via inbox; failure events trigger compensation
events.

- **Pros**
  - Maximum decoupling; survives orchestrator restarts.
- **Cons**
  - Wall-clock latency on a multi-module cascade goes from seconds to
    tens of seconds per step (Kafka round-trip). Operator UX
    ("uninstall and wait, then check back") regresses.
  - The cascade is rare (operator-initiated, not high-volume); saga
    machinery is overkill.
  - Failure compensation in a saga is logically the same forward-log
    pattern; we'd ship it on top of an extra layer of asynchrony.
  - **Rejected** — over-engineered for the cardinality.

### 3. Single shared connection across all module DbContexts

Bridge every module's DbContext to one shared `NpgsqlConnection` for
the duration of the cascade.

- **Pros**
  - Single transaction is once again technically possible.
- **Cons**
  - Breaks `BaseDbContext`'s schema-routing per tenant (the connection's
    `search_path` is global; per-DbContext `HasDefaultSchema` cannot
    serve two different default schemas on the same physical
    connection without race conditions on connection-level state).
  - Reintroduces all the implicit-state risks DbContext-per-module was
    designed to avoid.
  - **Rejected** — would silently corrupt schema routing for any
    concurrent non-cascade operation in the same scope.

### 4. Distributed transaction coordinator (XA / 2PC via Npgsql)

Use 2PC across N module-owned connections.

- **Pros**
  - Genuine cross-module atomicity.
- **Cons**
  - ADR-0001 explicitly rejects DTC as a platform primitive. Adopting
    it for one operation contaminates the platform's distributed-state
    model.
  - Postgres 2PC requires `prepared_transactions > 0` server-side and
    is rarely operated in production for performance reasons.
  - **Rejected** — violates ADR-0001 for a problem that does not
    require atomicity, only observability.

## Decision outcome

**Adopt Option 1.** The cascade uninstall mechanism in ADR-0028 §Decision
outcome is replaced by:

- One **session-scoped** `pg_advisory_lock(hashtext('uninstall:' || tenant_id))`
  acquired before the cascade and released in `finally`.
- One **transaction per module**, in reverse-dependency order.
- A **forward log** (`successes` list) maintained by the coordinator for
  the duration of the cascade.
- On failure at module N: rollback that module's transaction, emit a
  `ModuleUninstallFailedIntegrationEvent` carrying the successes-so-far
  and the failed module, throw `CascadePartialFailure` so the caller
  surfaces the partial state. **Earlier modules' uninstalls are NOT
  auto-undone** — the operator decides whether to retry, escalate, or
  reinstall the affected modules within the retention window.
- Module-specific compensation (beyond the rename-back primitive) is
  opt-in via overriding `OnUninstallAsync` and documenting the
  reversal contract.

T-026's acceptance criteria as currently filed (per-module transactions
+ forward log + compensation) align with this ADR; T-026 is updated to
cite ADR-0031 as the source of the transaction-boundary rule.

```mermaid
sequenceDiagram
    participant Op as Operator (CLI / admin UI)
    participant Cmd as UninstallModuleCommand
    participant Reg as IModule registry
    participant SLock as Postgres session advisory lock
    participant Mod as Per-module step (reverse-topo)
    participant Outbox as Outbox / Inbox
    participant Audit as AuditEntry

    Op->>Cmd: Uninstall(moduleName, --cascade)
    Cmd->>Reg: GetDependents(moduleName)
    Cmd->>SLock: pg_advisory_lock(hashtext('uninstall:' || tenantId))
    loop reverse-topological order
        Cmd->>Mod: BEGIN tx (module's DbContext) → OnUninstallAsync → rename → TenantModule.IsDeleted=true → outbox enqueue → COMMIT
        alt module ok
            Mod->>Outbox: ModuleUninstalledIntegrationEvent
            Mod->>Audit: success row
            Note over Cmd: append to forward log (successes)
        else module fails
            Mod->>Outbox: ModuleUninstallFailedIntegrationEvent (successes_so_far, failed_module)
            Mod->>Audit: failure row
            Note over Cmd: throw CascadePartialFailure;<br/>earlier modules stay uninstalled
        end
    end
    Cmd->>SLock: pg_advisory_unlock (in finally)
    Outbox-->>Op: deliver to admin notification surface
```

## Consequences

### Positive

- T-026 can be implemented faithfully without contradicting an Accepted
  ADR. The implementer cites ADR-0031 in code; reviewers trace the
  decision back to the ADR rather than to a status-log paragraph.
- The platform's "no DTC, modules own their DbContexts" invariants are
  preserved end-to-end.
- Partial failure is a first-class outcome, not a hidden state.
  `ModuleUninstallFailedIntegrationEvent` is the contract; operators
  see it, audit captures it, the admin UI can surface it.
- DDL lock duration matches the smallest atomic unit (one module),
  not the cascade chain — addresses ADR-0028's own large-tenant caveat
  by construction.

### Negative

- "Cascade is atomic" is no longer a true statement; documentation
  must teach operators that cascade is "best-effort sequential, with
  observable partial failure." A one-liner in
  [`docs/modules/tier-1-core/identity/SPEC.md`](../modules/tier-1-core/identity/SPEC.md) §Uninstall
  + the T-026 Status log capture this for the implementer.
- The `ModuleUninstallFailedIntegrationEvent` payload schema needs to
  be designed alongside the `ModuleUninstalledIntegrationEvent`
  extension ADR-0028 already mandated — both ship under T-026.

### Neutral

- ADR-0028's other decisions are unaffected. Retention window,
  cleanup job, GDPR escape hatch, dependency guard, extended event
  schema, retention config key all remain in force as written.

## Implementation notes

- Replace `pg_advisory_xact_lock` references in T-026 (and any other
  task that cites the ADR-0028 outer-transaction shape) with
  `pg_advisory_lock` (session-scoped) + a `finally`-block
  `pg_advisory_unlock`.
- `CascadePartialFailure` exception lives in
  `Nexora.SharedKernel.Abstractions.Modules` so both the orchestrator
  and the admin endpoint can catch it without taking an Identity
  module dependency.
- Audit entries for cascade steps follow the same `AuditEntry.Create`
  shape T-025 already documents for the purge job:
  - `Module = "Identity"`, `Operation = "module.uninstall.cascade"`,
    `OperationType = "Delete"`.
  - `IsSuccess` reflects the per-module step outcome.
  - `Metadata` JSON: `{ "tenantId", "module", "step": N, "successes_so_far": [...], "failure": "..." }`.
- The admin UI surface for `ModuleUninstallFailedIntegrationEvent` is
  filed inside T-026 (it was already implicit in its acceptance
  criteria — this ADR simply makes the consumer's existence
  non-optional).

## References

- [ADR-0001](0001-modular-monolith.md) — Modular Monolith Architecture (the
  no-DTC constraint this ADR honours).
- [ADR-0028](0028-module-uninstall-data-retention-contract.md) — partially
  superseded by this ADR (cascade transaction policy only).
- [`docs/architecture/MODULE_SYSTEM.md`](../architecture/MODULE_SYSTEM.md) — per-module
  DbContext boundary that drove this decision.
- [T-026](../analysis/tasks/phase-2/T-026.md) — the implementation task that
  surfaced the conflict; its current AC content matches this ADR.
