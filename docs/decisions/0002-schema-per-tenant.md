# ADR-002: Schema-per-Tenant Multi-Tenancy

## Status
Accepted

## Date
2026-03-19

## Context
Nexora must support multiple tenants (organizations) on a single deployment. Each tenant's data must be isolated for security, compliance (KVKK/GDPR), and operational independence (backup/restore per tenant).

Three common multi-tenancy strategies exist:
1. **Database-per-tenant**: Complete isolation, highest cost
2. **Schema-per-tenant**: Strong isolation within one database
3. **Shared schema (row-level)**: All tenants in same tables, filtered by `tenant_id` column

## Decision
We will use **Schema-per-Tenant** in PostgreSQL.

```
nexora_db (single database)
├── public schema        → system tables (tenants, modules, config)
├── tenant_isabet schema → all tables for Isabet Group
├── tenant_acme schema   → all tables for Acme Corp
└── tenant_xyz schema    → all tables for XYZ Foundation
```

### Implementation
1. Tenant resolution middleware extracts `tenant_id` from JWT claim
2. EF Core `DbContext` dynamically sets `search_path` to tenant schema
3. Migrations are applied per-schema (new tenant = create schema + apply all migrations)
4. Cross-tenant queries are impossible at the ORM level (no accidental data leaks)

### Multi-Organization within Tenant
Within a tenant's schema, organizations are separated by `organization_id` column:
```sql
-- Schema: tenant_isabet
SELECT * FROM contacts WHERE organization_id = 'ikf';   -- IKF contacts
SELECT * FROM contacts WHERE organization_id = 'academy'; -- Academy contacts
SELECT * FROM contacts WHERE organization_id IS NULL;    -- Shared contacts
```

## Consequences

### Positive
- Strong data isolation without the cost of separate databases
- Independent backup/restore per tenant (pg_dump with schema filter)
- No risk of cross-tenant data leaks at the query level
- Tenant-specific migrations possible (custom modules)
- PostgreSQL handles thousands of schemas efficiently
- Simpler compliance (data residency, right to delete = drop schema)

### Negative
- Schema management complexity (must apply migrations to all schemas)
- Connection pooling slightly more complex (schema switching per request)
- Cross-tenant reporting requires explicit multi-schema queries (for platform admin)
- More schemas = more objects in pg_catalog (manageable up to ~5000 tenants)

### Risks
- Migration failures on one schema could leave tenants in inconsistent state; mitigated by transactional migrations and per-tenant migration tracking
- Large tenant count (10k+) may strain PostgreSQL; at that scale, consider sharding to multiple databases

## Alternatives Considered

| Alternative | Pros | Cons | Why Rejected |
|------------|------|------|-------------|
| Database-per-tenant | Maximum isolation | High cost (connection per DB), complex provisioning | Over-engineered for expected tenant count (< 1000) |
| Shared schema (row-level) | Simplest implementation, best connection pool usage | Risky (one missed WHERE clause = data leak), slower queries (every query filtered), hard to backup/restore per tenant | Security risk too high for compliance-sensitive data |
| Hybrid (shared for small, dedicated for enterprise) | Flexible | Complex, two code paths | Unnecessary complexity at this stage |
