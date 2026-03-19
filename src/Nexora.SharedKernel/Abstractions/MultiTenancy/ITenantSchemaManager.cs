namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>
/// Manages PostgreSQL schema lifecycle for tenants.
/// Each tenant gets its own schema: tenant_{tenantId}
/// </summary>
public interface ITenantSchemaManager
{
    /// <summary>Creates the schema and runs all module migrations.</summary>
    Task CreateSchemaAsync(string schemaName, CancellationToken ct = default);

    /// <summary>Runs pending migrations for a specific module in a tenant schema.</summary>
    Task MigrateModuleAsync(string schemaName, string moduleName, CancellationToken ct = default);

    /// <summary>Runs pending migrations for ALL modules in a tenant schema.</summary>
    Task MigrateAllModulesAsync(string schemaName, CancellationToken ct = default);

    /// <summary>Drops the tenant schema and all its data. DESTRUCTIVE.</summary>
    Task DropSchemaAsync(string schemaName, CancellationToken ct = default);

    /// <summary>Checks if a tenant schema exists.</summary>
    Task<bool> SchemaExistsAsync(string schemaName, CancellationToken ct = default);
}
