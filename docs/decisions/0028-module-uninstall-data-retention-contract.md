# 0028 — Module uninstall data-retention contract

## Status

Proposed  <!-- Proposed | Accepted | Superseded by NNNN -->

## Date

2026-04-23

## Context

`UninstallModuleCommand` (Identity module) is the current surface for
removing a module from a tenant. Its behavior today:

1. Invoke `IModule.OnUninstallAsync(TenantInstallContext, ct)` on the
   module being removed — every module ships a `Task.CompletedTask`
   no-op.
2. Delete orphaned `role_permissions` rows that point at the uninstalled
   module's permissions (hard delete).
3. Rename every `{moduleName}_*` table in the tenant schema to
   `{tableName}_del_{timestamp}`.
4. Record the list of renamed tables as a CSV on
   `TenantModule.DeletedTableNames`.
5. Soft-delete the `TenantModule` row (`IsDeleted = true`).
6. Emit `ModuleUninstalledIntegrationEvent` via the outbox.

This works operationally but has never been formalized as a contract.
Several gaps surface once a module with PII (Contacts, CRM, Documents,
Fundraising) is uninstalled:

- **GDPR Article 17 gap.** Renamed `_del_{timestamp}` tables still hold
  the erased contact's data. A subsequent
  `ContactGdprDeletedIntegrationEvent` consumer (T-004) looks up the
  canonical table name and finds nothing — the rename effectively hides
  the data from erasure without deleting it. This is *technically* a
  retention-longer-than-declared failure.
- **No retention policy.** The `_del_{timestamp}` tables live forever.
  There is no job that drops them after N days. Disk usage grows
  unbounded for tenants who churn modules.
- **No cross-module signal.** `ModuleUninstalledIntegrationEvent` exists
  but no module subscribes. CRM/Documents may hold `contactId`
  references to a contact module that has just been uninstalled — those
  references become dangling without anyone noticing.
- **Reinstall path undocumented.** `DeletedTableNames` stores the CSV
  but no code reads it to rename back. A reinstall today creates new
  empty tables alongside the `_del_` copies; the renamed data is
  silently abandoned.
- **Cascade policy unclear.** Can you uninstall Contacts while CRM
  (which depends on it) is still installed? Today, yes — the command
  does not consult `IModule.Dependencies`. CRM's handlers then fail at
  runtime with `relation "contacts_contacts" does not exist`.

Phase 2 introduces Tier-2 modules (CRM, Finance, Subscription) whose
cross-module data volume makes every gap above materially worse. The
contract has to be formalized before Tier-2 ships, not after.

## Decision drivers

- GDPR Article 17 compliance — every retained row that holds personal
  data MUST be governed by a declared, auditable retention window.
  "Rename and forget" is not a retention window.
- Operational predictability — SREs must be able to answer "what
  actually happens when a tenant uninstalls module X?" without reading
  the handler source.
- Cross-module safety — dependent modules MUST receive a signal they
  can act on before the data they reference becomes unreachable.
- Reversibility — an accidental uninstall should be recoverable inside
  the retention window without escalating to a DB admin.
- Tenant sovereignty — operators (tenant admins) MUST be able to
  override the retention window within compliance-cap bounds
  (ADR-0025).

## Considered options

### 1. Contract-first: formalize the rename-and-retain pattern with a mandatory cleanup job *(chosen)*

Keep the current rename mechanism; wrap it in an explicit contract:

- Uninstall is a **reversible retention-bounded** operation. Within the
  retention window, reinstall restores data via the
  `DeletedTableNames` CSV.
- Default retention window is **30 days** per uninstalled module per
  tenant. Configurable via the resolver key
  `modules.uninstall.retention_days`
  (tenant default, org override possible, capped by platform).
- A recurring Hangfire job `platform:purge-uninstalled-modules` scans
  `TenantModule` soft-deleted rows past the retention window, drops
  the matching `_del_{timestamp}` tables, erases the `DeletedTableNames`
  CSV (so it cannot accidentally be read later), and hard-deletes the
  `TenantModule` row.
- `OnUninstallAsync(TenantInstallContext, ct)` gets a companion
  `OnReinstallAsync(TenantInstallContext, ct)` contract. Defaults are
  no-ops; modules that need to do bookkeeping (e.g. Notifications
  re-registering template subscriptions) override.
- Uninstall consults `IModule.Dependencies` and refuses to remove a
  module that another installed module depends on. Forcing past the
  guard requires the dependent module to be uninstalled first or a
  `--cascade` flag that uninstalls them all under one transactional
  unit.
- `ModuleUninstalledIntegrationEvent` is extended with the list of
  affected canonical table names; consumers in other modules can
  subscribe via the standard inbox guard and prune their own
  cross-references. Non-adoption stays safe — consumers that ignore the
  event see dangling references, exactly as today.
- **GDPR erasure path** (T-004 / ADR-0008) gets an explicit escape
  hatch: when the erasure handler encounters a `_del_{timestamp}`
  renamed table, it queries the renamed table, erases the matching row,
  and logs the compliance action. The retention window does not trump
  Article 17.

- Pros
  - Preserves the reinstall-within-30-days operator ergonomic that the
    current rename pattern was trying to offer but never documented.
  - Closes the GDPR gap without requiring immediate hard-delete on
    uninstall.
  - Cascade + dependency guard eliminates the "uninstall Contacts,
    CRM breaks" class of incident.
  - Compliance-cap provider (ADR-0025) can force-shorten the retention
    window for EU tenants without code changes.
  - Cleanup is a single recurring job — operational surface is small
    and observable via a standard Hangfire dashboard.
- Cons
  - Existing code ships uninstall without retention-job wiring;
    implementing the cleanup job (T-025 follow-up) is mandatory, not
    optional.
  - Modules with PII-bearing tables need to list their tables so the
    GDPR escape hatch can find them — declaration discipline is
    required (architecture test in the follow-up task).
  - Cross-module consumers of the extended event need wiring; absent
    wiring is safe but not complete.

### 2. Immediate hard-delete on uninstall

Skip the rename. Uninstall drops the tables, hard-deletes the
`TenantModule` row, and emits the event. No retention, no reinstall
recovery.

- Pros
  - Simplest mental model; data is gone, same as it says on the tin.
  - Closes the GDPR gap by construction — nothing is retained.
- Cons
  - Zero recovery for operator error. An accidental uninstall is
    data loss.
  - Tier-2 modules (Finance, Subscription) have multi-million-row
    tables; an immediate drop inside a DB transaction is an
    operational risk (long lock, replication lag).
  - Breaks the current reinstall-via-DeletedTableNames expectation that
    some tests rely on. **Rejected** — the operational downside is
    real and the reversibility property is load-bearing.

### 3. Soft-delete only, no table rename, no cleanup job

Leave tables in place; just set `TenantModule.IsDeleted = true` and
suppress module code from loading. Module tables remain accessible to
the DB but unused.

- Pros
  - Zero data-loss risk.
  - No cleanup job needed.
- Cons
  - Data grows forever. Tier-2 multi-tenant disk usage becomes
    unpredictable.
  - Module tables still collide on reinstall (same table names in the
    tenant schema).
  - GDPR erasure is easy to miss — the row is neither renamed nor
    isolated, it's just attached to an "uninstalled" flag on a
    different table. Erasure handlers that short-circuit on
    `!module.IsInstalled` skip the data.
  - **Rejected** — fails the retention-window property.

### 4. Per-module strategy (module declares its own uninstall policy)

Each module implements `UninstallPolicy` (delete, rename-and-keep-N-days,
archive-to-cold-storage, etc.) and the uninstall pipeline honours
whichever it chose.

- Pros
  - Maximum flexibility. A Documents module could push to S3-glacier on
    uninstall; a Contacts module could hard-delete.
- Cons
  - Policy sprawl — every module owner learns a new API.
  - Cross-module reasoning breaks: "what happens when I uninstall
    Contacts while CRM is installed?" depends on Contacts' policy AND
    CRM's response.
  - Rejected — the uniform 30-day window is more valuable than
    per-module tailoring for the foreseeable horizon. A future ADR can
    add opt-in policy variants once concrete modules demand them.

## Decision outcome

**Adopt Option 1** — a formalized rename-and-retain contract with:

- 30-day default retention, configurable via
  `modules.uninstall.retention_days`.
- Mandatory recurring cleanup job `platform:purge-uninstalled-modules`.
- Dependency-cascade guard at uninstall time.
- Extended `ModuleUninstalledIntegrationEvent` carrying canonical table
  names for cross-module consumers.
- GDPR erasure escape hatch that honours Article 17 inside the
  retention window.

This preserves the reinstall-within-30-days ergonomic the current
rename was silently providing, closes the GDPR gap, and sets a clear
boundary around the existing code without rewriting it. Implementation
is split into follow-up tasks (T-025 cleanup job, T-026
dependency-cascade guard, T-027 GDPR-retention-window handler, …); this
ADR formalizes *what*, those tasks ship *how*.

## Consequences

### Positive

- Uninstall behavior is documented; operators and SREs can answer
  "what happens when I uninstall X?" from the ADR.
- GDPR Article 17 continues to hold post-uninstall — the escape hatch
  prevents the rename from becoming a data-protection loophole.
- Tier-2 disk growth is bounded by the retention window, not unbounded.
- Cascade guard eliminates a known class of broken-state incidents.
- Compliance-cap coordination with ADR-0025 is explicit.

### Negative

- Three follow-up tasks (cleanup job, cascade guard, GDPR handler)
  need to ship before the contract is fully in force. Until they do,
  this ADR is Accepted-but-incomplete; the follow-up backlog tracks
  the gap.
- Modules that carry PII must declare their canonical table names on
  uninstall (the event carries this) so the GDPR escape hatch finds
  them. Declaration discipline is required.
- The `DeletedTableNames` CSV-on-a-row pattern is kept — it's not
  elegant, but splitting it into a separate table would be a
  migration-heavy refactor with no behavioral benefit.

### Neutral

- `IModule.OnUninstallAsync` gets a companion
  `OnReinstallAsync(TenantInstallContext, ct)` method (default no-op via
  C# default interface method, same pattern as T-005's
  `SeedDemoDataAsync`). Existing modules compile unchanged.
- The retention key `modules.uninstall.retention_days` joins the
  growing list of compliance-cap-governed tenant settings; the
  admin compliance settings page (T-019) surfaces it alongside
  `gdpr.hard_delete.enabled`.

## Implementation notes

Concrete pointers split into follow-up tasks:

- **T-025 — `platform:purge-uninstalled-modules` Hangfire job**:
  - Queue: `maintenance`.
  - Cron: daily at 03:00 UTC per tenant, scanning
    `PlatformDbContext.TenantModules` where `IsDeleted = true AND
    (DeletedAt + retention) < now()`.
  - For each: drop every table in `DeletedTableNames`, null the CSV
    field, hard-delete the `TenantModule` row.
  - Observability: counter
    `nexora_module_uninstall_purged_total{module}` + histogram
    `nexora_module_uninstall_purge_duration_seconds`.
- **T-026 — Cascade guard in `UninstallModuleCommand`**:
  - Before uninstalling, consult `IEnumerable<IModule>` and refuse when
    an installed dependent remains. Error lockey
    `lockey_identity_error_module_uninstall_blocked_by_dependent`.
  - `--cascade` boolean on the command wraps the pre-order walk in a
    transaction; each module uninstalls in reverse-dependency order.
- **T-027 — GDPR erasure escape hatch**:
  - `ContactGdprDeletedIntegrationEventHandler` (and its sibling
    handlers in CRM / Documents / Subscription when those land) gains
    a second lookup pass: after the canonical-table redaction, query
    every table in the tenant that matches
    `{module}_{table}_del_%` and apply the same redaction there.
  - Architecture test: modules that handle
    `ContactGdprDeletedIntegrationEvent` MUST implement the escape
    hatch; absence trips a CI guard.
- **Extended `ModuleUninstalledIntegrationEvent`**:
  - Adds `CanonicalTableNames` (list of string) + `RenamedTableNames`
    (list of string) + `UninstalledAtUtc` (DateTimeOffset) fields.
  - Backward-compatible: existing consumers read `TenantId` +
    `ModuleName` only.
- **Retention config key**:
  - Name: `modules.uninstall.retention_days`.
  - Type: `int`.
  - Default: 30. Range: 7–365 (platform cap).
  - Surfaced on `/identity/settings/compliance` (T-019 admin page).
- **Docs updates on Accept**:
  - `docs/modules/tier-1-core/identity/SPEC.md` §Uninstall: replace
    current terse note with the contract surface.
  - `docs/standards/audit-coverage.md` §2 adds an uninstall entry —
    the `UninstallModuleCommand` handler already logs; formalize that
    the renamed-table drops by the purge job are `MUST AUDIT`
    (compliance-relevant, retention-driven).

## References

- ADR-0001 — Modular Monolith Architecture.
- ADR-0002 — Schema-per-Tenant Multi-Tenancy.
- ADR-0008 — GDPR Deletion Strategy.
- ADR-0025 — Org-Scoped Compliance Config with Platform Caps.
- `src/Modules/Nexora.Modules.Identity/Application/Commands/UninstallModuleCommand.cs`
  — current behavior this ADR formalises.
- T-004 — Contacts GDPR hard-delete (produces the events the escape
  hatch must honour).
- T-005 — `IModule.SeedDemoDataAsync` default-impl pattern reused here
  for the new `OnReinstallAsync` hook.
- External: GDPR Article 17 "Right to Erasure".
