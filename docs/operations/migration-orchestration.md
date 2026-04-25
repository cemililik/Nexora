# Migration Orchestration Runbook

**Derives from:** [ADR-0002](../decisions/0002-schema-per-tenant.md) (Schema-per-Tenant Multi-Tenancy), [ADR-0003](../decisions/0003-deployment-strategy.md) (Deployment Strategy), [ADR-0027](../decisions/0027-production-schema-migration-strategy.md) (Production Schema-Migration Strategy — the ADR that formalises the EF-Core-migrations + release-gate dev-to-prod bridge this runbook operationalises).

Operational guide for applying EF Core migrations across all tenant schemas.

## 1. Migration lifecycle

### 1.1 Module-level migrations
Each module has its own `DbContext` and owns a migration history table `__ef_migrations_{module}` within each tenant schema. The `public` schema holds only platform tables (`tenants`, `tenant_config`, `outbox_messages_platform`).

### 1.2 Additive-only rule
Per ADR-0003, production migrations MUST be additive:
- Adding columns: allowed (must be nullable or have DB default)
- Adding tables, indexes: allowed
- Adding foreign keys: allowed if the target column existed >=1 prior version
- Dropping columns / tables: blocked by CI architecture test `AdditiveOnlyMigrationTest`
- Renaming columns: blocked - use add-new + backfill + drop-in-N+2 pattern
- Changing column types with data loss: blocked

Enforcement: `tests/Nexora.Architecture.Tests/MigrationTests.cs` parses every `Migration.Up` method and fails on `DropColumn`, `RenameColumn`, or type-narrowing `AlterColumn`.

## 2. Applying migrations

### 2.1 New tenant provisioning
Flow: NMP creates tenant -> `TenantProvisioner` creates schema (`CREATE SCHEMA tenant_{id}`) -> `MigrationRunner.MigrateAllModulesAsync(tenantId)` applies every module's current migration head to that schema.

All-or-nothing: if any module's migration fails, the schema is rolled back (`DROP SCHEMA tenant_{id} CASCADE`) and the tenant is marked `provisioning_failed`.

### 2.2 Platform upgrade (rolling tenant migration)
Flow:
1. Deploy new Nexora release (Helm upgrade).
2. Hangfire recurring job `platform:migrate-tenants` fires automatically post-deploy (triggered by `DeploymentCompleted` event).
3. Job iterates tenants in pages of 50, applying migrations per tenant concurrently (max 10 parallel).
4. Per-tenant: acquire advisory lock `pg_advisory_lock(hashtext('migrate:' || tenant_id))` -> apply each module's pending migrations in module dependency order -> release lock.
5. On per-tenant failure: tenant is marked `migration_failed`, logged to `platform_migration_failures`, and the tenant is quarantined (requests return 503).
6. Successful tenants update `tenants.schema_version` to the new target version.

### 2.3 Failure recovery
Quarantined tenants do NOT auto-retry. Ops workflow:
1. Inspect failure: `SELECT * FROM platform_migration_failures WHERE tenant_id = X`.
2. Reproduce against a staging clone of the tenant's schema.
3. Fix the migration or data, push hotfix release.
4. Manually trigger `platform:retry-tenant-migration` job via admin API or NMP UI.
5. On success, tenant `status` returns to `active` and quarantine is lifted.

## 3. Drift detection

### 3.1 `schema_version` per tenant
The `tenants` table has a `schema_version` (varchar) column tracking the last successfully applied version (semver of the platform release).

### 3.2 Nightly drift audit
Recurring job `platform:audit-migration-drift` runs at 05:00 UTC daily:
- For every tenant, query `__ef_migrations_{module}` for each module and compute the actual applied head.
- Compare against expected head (derived from assembly version).
- Log drift to `platform_migration_drift` and emit `platform.migration.drift.detected` event (consumed by ops alerting).

### 3.3 Expected drift window
During a rolling platform migration, drift is expected for up to 2 hours. The audit job suppresses alerts for tenants whose migration started within the last 2h.

## 4. `search_path` and connection pooling

### 4.1 Pattern
Nexora uses Npgsql with connection pooling. Connection reuse creates a risk: a pooled connection might carry a previous tenant's `search_path` into a new request.

### 4.2 Mitigation
Every DbContext instance sets `search_path` in `OnConfiguring` via a connection-opened hook:
```csharp
optionsBuilder.UseNpgsql(connectionString, npgsql =>
    npgsql.RegisterConnectionOpenedHandler(async (conn) =>
    {
        var tenantId = _tenantContextAccessor.Current.TenantId;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"tenant_{tenantId}\", public";
        await cmd.ExecuteNonQueryAsync();
    }));
```
The handler fires on every connection open (including pool reuse). Architecture test `SearchPathIsolationTest` verifies no DbContext bypasses this hook.

### 4.3 Reset on close
`search_path` is explicitly reset to `public` on connection close (same hook, opposite direction) - defense in depth.

## 5. Soft-delete + schema-per-tenant

EF Core global query filters on `AuditableEntity<T>` (`WHERE IsDeleted = false`) operate within a tenant's schema. Since schema isolation is physical, cross-tenant queries are impossible regardless of filter state. The filter exists for soft-delete semantics within the tenant, not for tenant isolation.

Use `IgnoreQueryFilters()` for admin audit views - but still only sees within the caller's tenant schema. Cross-tenant audit (platform admin) requires `search_path = 'public', 'tenant_*'` which is restricted to `platform.admin` permission.

## 6. Scaling considerations

### 6.1 5K tenant soft limit
PostgreSQL can host thousands of schemas; at ~5K tenants per physical DB, the `pg_catalog` query plans degrade and schema lookup time rises. Trigger for sharding evaluation:
- `pg_catalog.pg_namespace` row count > 5000, OR
- Median connection-open time > 100ms, OR
- Total schema size > 500 GB.

Any of the above triggers a sharding review (splitting tenants across multiple physical DBs).

### 6.2 Per-tenant advisory locks
Migration concurrency uses `pg_advisory_lock` keyed on tenant ID. Lock contention caps effective concurrent migrations to the connection pool size - typically 100. For > 1000 tenants, batch size of 50 with 10 parallel keeps total migration window under 1 hour.

## 7. Rollback

### 7.1 Additive-only implies no rollback migration
Because migrations are additive, there is no automatic rollback. If a release is bad, the ops path is:
1. Roll forward a hotfix release.
2. Never run `Migration.Down` in production.

### 7.2 Data rollback
For data issues (bad backfill), write a targeted data migration in the next release - do not use EF migration rollback.

## 8. References

- ADR-0002 - Schema-per-Tenant Multi-Tenancy
- ADR-0003 - Deployment Strategy
- ADR-0008 - GDPR Deletion Strategy (schema DROP as erasure)
- CLAUDE.md §Database - additive-only rule
- CLAUDE.md §Infrastructure Standards §Cache - DaprCacheService tenant prefix

## 8.A Uninstall purge

Module uninstall renames tables to `{module}_{table}_del_{timestamp}` rather than dropping them ([ADR-0028](../decisions/0028-module-uninstall-data-retention-contract.md)). The `platform:purge-uninstalled-modules` Hangfire job (T-025) is what eventually expunges those tables once they age past the retention window.

- **Schedule.** One platform-level recurring registration at `0 3 * * *` UTC. The outer run enumerates tenants with eligible `TenantModule` rows and fans per-tenant child runs out via `BackgroundJob.Schedule` with a stable jitter offset of 0–119 minutes (`MD5(tenantId) & 0x7FFFFFFF % 120`). Different tenants land in different minute slices so concurrent `DROP TABLE` statements don't thundering-herd Postgres WAL writers.
- **Retention.** Default 30 days; per-tenant override via `IConfigurationResolver.GetAsync<int?>("modules.uninstall.retention_days")` subject to the platform compliance cap (ADR-0029).
- **Per-row body.** Parses `TenantModule.DeletedTableNames` (CSV) via `ParseDeletedTableNames`, validates each entry against `^[a-z][a-z0-9_]*_del_(?:[0-9]{14}|[0-9]{8}_[0-9]{6})$` (the regex accepts both timestamp shapes T-026 may emit), drops surviving entries one at a time under their own try/catch, reserializes the failed list, and hard-deletes the `TenantModule` row only when nothing is left to retry.
- **Observability.** Counter `nexora_module_uninstall_purged_total{module}` + histogram `nexora_module_uninstall_purge_duration_seconds`. One audit entry per row (`Module=Identity`, `Operation=module.uninstall.purge`, `OperationType=Delete`, `UserEmail=system:platform-purge`) with structured `Metadata` carrying the dropped/failed lists; success is `failed.Count == 0 && rowHardDeleted` per the AC.
- **Tuning.** Lower the retention via the per-tenant config override (cap-bounded). Force a rerun by triggering the recurring job from the Hangfire dashboard.

## 9. Task references

- T-011 (Phase 2 Milestone A) - `MigrationRunner.MigrateAllModulesAsync` implementation
- T-012 (Phase 2 Milestone A) - Architecture test `AdditiveOnlyMigrationTest`
- T-013 (Phase 2 Milestone B) - `platform:audit-migration-drift` Hangfire job
- T-025 (Phase 2 Milestone A) - `platform:purge-uninstalled-modules` Hangfire job (this §8.A)
- T-026 (Phase 2 Milestone A) - cascade-uninstall orchestrator (ADR-0031)
