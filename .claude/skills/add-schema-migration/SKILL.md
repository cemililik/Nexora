---
name: add-schema-migration
description: Add an incremental schema change (new column, table, or index) to DevelopmentSeed.cs following the Nexora migration-free pattern. Use when the user adds a new property to an entity, asks to "add a column", "migrate the database", "add a table", or the app crashes with a missing column error.
---

# Add Schema Migration

Nexora uses **no EF Core migration files**. All schema changes after initial table creation go into `src/Nexora.Host/DevelopmentSeed.cs` → `ApplySchemaUpdatesAsync` → `alterStatements` array.

Full rules: `docs/standards/schema-migration.md`.

---

## Step 1 — Identify the change

Ask yourself (or the user):

| Question | Answer determines |
|----------|------------------|
| New column on existing table? | Add `ADD COLUMN IF NOT EXISTS` |
| New table on a module that already ran Phase 1? | Add `CREATE TABLE IF NOT EXISTS` |
| New index? | Add `CREATE INDEX IF NOT EXISTS` |
| New property on an entity whose module table was just created? | Nothing needed — `CreateTablesAsync()` handles it |

If unsure whether Phase 1 already ran, check if the module's sentinel table exists in the dev schema:
```sql
SELECT to_regclass('"tenant_00000000-0000-0000-0000-000000000001".{module}_{sentinel}');
```

---

## Step 2 — Write the SQL statement

### New nullable column
```csharp
// {Entity}.{Property} — {reason}
"ALTER TABLE {module}_{table} ADD COLUMN IF NOT EXISTS \"{ColumnName}\" {pg_type} NULL",
```

### New NOT NULL column (must have DEFAULT for back-fill)
```csharp
// {Entity}.{Property} — {reason}
"ALTER TABLE {module}_{table} ADD COLUMN IF NOT EXISTS \"{ColumnName}\" {pg_type} NOT NULL DEFAULT {default_value}",
```

### New table
```csharp
// {reason}
"""
CREATE TABLE IF NOT EXISTS {module}_{table} (
    "Id" uuid PRIMARY KEY,
    ...
    "CreatedAt" timestamptz NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
)
""",
```

### New index
```csharp
"CREATE INDEX IF NOT EXISTS \"IX_{module}_{table}_{Column}\" ON {module}_{table} (\"{Column}\")",
```

### Type mapping

| C# type | PostgreSQL |
|---------|-----------|
| `Guid?` | `uuid NULL` |
| `Guid` | `uuid NOT NULL` |
| `string` bounded | `varchar(N)` |
| `string` unbounded | `text` |
| `bool` | `boolean NOT NULL DEFAULT false` |
| `DateTimeOffset?` | `timestamptz NULL` |
| `DateTimeOffset` | `timestamptz NOT NULL` |
| `decimal` | `numeric(18,4)` |
| `enum` as string | `varchar(50)` |
| `JsonDocument` | `jsonb` |

---

## Step 3 — Add to DevelopmentSeed.cs

File: `src/Nexora.Host/DevelopmentSeed.cs`
Method: `ApplySchemaUpdatesAsync`
Location: **append to the end of the `alterStatements` array**, before the closing `}`.

```csharp
var alterStatements = new[]
{
    // ... existing statements ...

    // {Entity}.{Property} — {reason}         ← your comment
    "ALTER TABLE ...",                          ← your SQL
};
```

---

## Step 4 — Apply to running dev database

If the dev postgres container is already running with an existing schema, apply the column manually so you don't need a full reset:

```bash
docker compose exec postgres psql -U nexora -d nexora -c \
  'ALTER TABLE "tenant_00000000-0000-0000-0000-000000000001".{module}_{table} ADD COLUMN IF NOT EXISTS "{ColumnName}" {pg_type} NULL;'
```

Then rebuild and restart the API:
```bash
docker compose build nexora-api && docker compose up -d
```

---

## Step 5 — Verify idempotency

Restart the API a second time without resetting the database. If the seed completes without errors, the change is idempotent:

```bash
docker compose restart nexora-api
docker compose logs nexora-api --tail=30
```

Look for `[DevSeed] Schema updates applied` with no exception stack traces.

---

## Checklist

- [ ] C# entity property added
- [ ] EF configuration added (if non-conventional)
- [ ] `alterStatements` entry added with a one-line comment
- [ ] Column type matches C# → PostgreSQL mapping table
- [ ] `NOT NULL` column has a `DEFAULT` for back-fill
- [ ] Applied manually to running dev DB (if schema already exists)
- [ ] Idempotency verified (second restart is clean)
