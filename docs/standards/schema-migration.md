# Schema Migration Standard

Nexora uses a **code-first, migration-free** schema management approach for the dev
and on-prem-preview environments. There are no EF Core migration files yet; schema
evolution happens in two distinct phases, both enforced inside `DevelopmentSeed.cs`.

> **Production strategy — defined.** The mechanisms described here are explicitly
> **development-only**: `DevelopmentSeed.SeedAsync` is guarded by
> `app.Environment.IsDevelopment()` and must never run against production tenants.
> Production schema evolution uses **EF Core migrations** per
> [ADR-0027](../decisions/0027-production-schema-migration-strategy.md)
> (Accepted 2026-04-24), orchestrated by the `MigrationRunner` described in
> [operations/migration-orchestration.md](../operations/migration-orchestration.md).
> A CI release-gate asserts every DDL line in `ApplySchemaUpdatesAsync` has a
> matching EF migration before a release cut. Any ALTER intended for production
> MUST travel through that pipeline, not through `ApplySchemaUpdatesAsync`.

---

## 0. At a Glance

| Phase | When | Mechanism | Location | Runs in |
|-------|------|-----------|----------|---------|
| **Phase 1 — Initial creation** | First startup, table does not exist yet | `IRelationalDatabaseCreator.CreateTablesAsync()` | `EnsureIdentityTablesAsync` / `EnsureModuleTablesAsync<T>` | **Development-only** |
| **Phase 2 — Incremental changes** | Any subsequent startup | Raw SQL in `ApplySchemaUpdatesAsync` | `alterStatements` array inside that method | **Development-only** |

**Every change MUST be idempotent.** The seed runs on every container restart.

---

## 1. Phase 1 — Adding an Entirely New Module Table

When you add a **new entity** to an existing module's `DbContext`, no action is needed in `DevelopmentSeed.cs`. The `EnsureModuleTablesAsync<T>` call already runs `CreateTablesAsync()` against the full DbContext model, which picks up all mapped entities. The check is:

```csharp
// Only runs CreateTablesAsync if the sentinel table does not yet exist
var exists = await TableExistsAsync(conn, SchemaName, "module_sentinel_table");
```

The sentinel is the first/primary table of that module (e.g. `contacts_contacts`, `documents_documents`). If it exists, Phase 1 is skipped entirely — the module is considered fully initialised from a previous run.

**This means**: a brand-new entity added to a DbContext that has already run Phase 1 will NOT be created automatically. You must add a Phase 2 `CREATE TABLE IF NOT EXISTS` statement instead (see §2).

---

## 2. Phase 2 — Incremental Changes

All changes after the initial `CreateTablesAsync` go into the `alterStatements` array inside `ApplySchemaUpdatesAsync`. This runs on every startup in order, each statement wrapped in an individual `try/catch`.

### Rules

1. **Always idempotent** — use `ADD COLUMN IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, `CREATE UNIQUE INDEX IF NOT EXISTS`. Never bare `ALTER TABLE` without `IF NOT EXISTS`.
2. **No destructive DDL** — never `DROP COLUMN`, `DROP TABLE`, `ALTER COLUMN` changing type or nullability to stricter, or `TRUNCATE`. Destructive ops require an ADR and explicit migration script.
3. **Comment every statement** — one line above the SQL explaining *what* is being added and *why*:
   ```csharp
   // User.ContactId — optional link to Contacts module for 360° view
   "ALTER TABLE identity_users ADD COLUMN IF NOT EXISTS \"ContactId\" uuid NULL",
   ```
4. **Order matters** — if a new table references another new table via FK, add the referenced table first in the array.
5. **Column defaults must be backward-compatible** — `NOT NULL` columns must carry a `DEFAULT` value so existing rows are back-filled without locking the table.
6. **Schema qualifier is set once** — `SET search_path` is called at the top of `ApplySchemaUpdatesAsync`. Use plain unqualified table names inside `alterStatements`.

### What goes where

| Change type | Where |
|-------------|-------|
| New nullable column on existing table | `alterStatements` — `ADD COLUMN IF NOT EXISTS ... NULL` |
| New NOT NULL column on existing table | `alterStatements` — `ADD COLUMN IF NOT EXISTS ... NOT NULL DEFAULT ...` |
| New index | `alterStatements` — `CREATE INDEX IF NOT EXISTS ...` |
| New unique index | `alterStatements` — `CREATE UNIQUE INDEX IF NOT EXISTS ...` |
| New table (module already initialised) | `alterStatements` — `CREATE TABLE IF NOT EXISTS ...` |
| New table (brand-new module) | Handled automatically by `EnsureModuleTablesAsync<T>` — no entry needed |
| Data backfill / UPDATE | `alterStatements` — after the `ALTER TABLE` it depends on |

### Template

```csharp
// {Entity}.{Property} — {one-line reason}
"ALTER TABLE {module}_{table} ADD COLUMN IF NOT EXISTS \"{ColumnName}\" {pg_type} {NULL|NOT NULL DEFAULT ...}",
```

Examples from the codebase:

```csharp
// User.PreferredLanguage — BCP 47 language tag for UI display preference (null = use tenant default)
"ALTER TABLE identity_users ADD COLUMN IF NOT EXISTS \"PreferredLanguage\" varchar(10)",

// Organization.DefaultLocale — IETF locale tag for org-level number/date formatting
"ALTER TABLE identity_organizations ADD COLUMN IF NOT EXISTS \"DefaultLocale\" varchar(20) NOT NULL DEFAULT 'en-US'",

// Permission.Scope — classifies permissions as Platform or Tenant scope
"ALTER TABLE identity_permissions ADD COLUMN IF NOT EXISTS \"Scope\" varchar(20) NOT NULL DEFAULT 'Tenant'",
```

---

## 3. EF Entity ↔ Database Sync Checklist

When you add a property to a domain entity:

- [ ] Property added to the C# entity class
- [ ] EF configuration added (if non-conventional — e.g. `HasMaxLength`, `HasConversion`)
- [ ] `alterStatements` entry added in `ApplySchemaUpdatesAsync`
- [ ] Comment explains *why* the column was added
- [ ] Column type matches EF's expected mapping (see §4)
- [ ] If `NOT NULL`, a `DEFAULT` is provided for back-fill

---

## 4. Type Mapping Reference

| C# type | PostgreSQL type |
|---------|----------------|
| `Guid` / `Guid?` | `uuid` / `uuid NULL` |
| `string` (bounded) | `varchar(N)` |
| `string` (unbounded) | `text` |
| `int` | `int` |
| `bool` | `boolean NOT NULL DEFAULT false` |
| `DateTimeOffset` | `timestamptz` |
| `DateTimeOffset?` | `timestamptz NULL` |
| `decimal` | `numeric(18,4)` |
| `enum` (stored as string) | `varchar(50)` |
| `JsonDocument` / `object` | `jsonb` |

---

## 5. Testing

1. **Idempotency** — restart the API container (`docker compose restart nexora-api`) and confirm no errors. The seed must be re-runnable without failure.
2. **First-run** — `docker compose down -v && docker compose up` simulates a fresh database. Phase 1 creates tables; Phase 2 applies all incremental changes in order.
3. **Column presence** — confirm via psql or pgAdmin after each restart:

   ```sql
   \d "tenant_00000000-0000-0000-0000-000000000001".{module}_{table}
   ```

4. **Drift detector** — [`tools/check-schema-drift.py`](../../tools/check-schema-drift.py)
   compares every EF entity's mapped properties against the live dev tenant
   schema and exits non-zero on drift. T-024 wraps the script in an opt-in
   xUnit test
   ([`SchemaDriftToolTests`](../../tests/Nexora.Infrastructure.Tests/Tooling/SchemaDriftToolTests.cs),
   tagged `Category=Tooling`), skipped unless the
   `NEXORA_SCHEMA_DRIFT_ENABLED=1` environment variable is set. Nightly CI and
   local devs opt in; default PR CI stays fast. When the script reports drift,
   the test fails with the full stdout so the offending column / entity is
   visible in the CI log.

---

## 6. What NOT to Do

- **Do not** run `dotnet ef migrations add` against the dev branch without also landing the matching `ApplySchemaUpdatesAsync` entry — the production migration pipeline per [ADR-0027](../decisions/0027-production-schema-migration-strategy.md) pairs the two. Dev remains code-first through `ApplySchemaUpdatesAsync`; EF migration files are additive, produced alongside (not instead of) the dev path, and the CI release-gate enforces the pairing.
- **Do not** modify the database manually without a matching code change in `ApplySchemaUpdatesAsync`; the next fresh-start will be out of sync.
- **Do not** use `EnsureCreated()` or `Database.EnsureCreatedAsync()` — these are reserved for the Host-level seeder only.
- **Do not** add production DDL in `DevelopmentSeed.cs` beyond what runs in Development mode — this file is guarded by `if (!app.Environment.IsDevelopment()) return;`. Production schema management is governed by [ADR-0027](../decisions/0027-production-schema-migration-strategy.md) and operationalised in [operations/migration-orchestration.md](../operations/migration-orchestration.md).
