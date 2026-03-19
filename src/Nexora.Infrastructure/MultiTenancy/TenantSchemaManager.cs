using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Npgsql;

namespace Nexora.Infrastructure.MultiTenancy;

/// <summary>
/// Manages PostgreSQL schema lifecycle for multi-tenancy.
/// Creates schemas, runs module migrations, and handles cleanup.
/// </summary>
public sealed class TenantSchemaManager(
    string connectionString,
    IEnumerable<IModuleMigration> moduleMigrations,
    ILogger<TenantSchemaManager> logger) : ITenantSchemaManager
{
    public async Task CreateSchemaAsync(string schemaName, CancellationToken ct = default)
    {
        ValidateSchemaName(schemaName);

        logger.LogInformation("Creating tenant schema '{Schema}'", schemaName);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // CREATE SCHEMA IF NOT EXISTS (schema name is validated, safe to interpolate)
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"";
        await cmd.ExecuteNonQueryAsync(ct);

        // Run all module migrations for the new schema
        await MigrateAllModulesAsync(schemaName, ct);

        logger.LogInformation("Tenant schema '{Schema}' created with {ModuleCount} module(s) migrated",
            schemaName, moduleMigrations.Count());
    }

    public async Task MigrateModuleAsync(string schemaName, string moduleName, CancellationToken ct = default)
    {
        var migration = moduleMigrations.FirstOrDefault(m => m.ModuleName == moduleName)
            ?? throw new InvalidOperationException($"No migration registered for module '{moduleName}'.");

        logger.LogInformation("Migrating module '{Module}' in schema '{Schema}'", moduleName, schemaName);

        await migration.MigrateAsync(schemaName, ct);
        await migration.SeedAsync(schemaName, ct);
    }

    public async Task MigrateAllModulesAsync(string schemaName, CancellationToken ct = default)
    {
        foreach (var migration in moduleMigrations)
        {
            logger.LogInformation("Migrating module '{Module}' in schema '{Schema}'",
                migration.ModuleName, schemaName);

            await migration.MigrateAsync(schemaName, ct);
            await migration.SeedAsync(schemaName, ct);
        }
    }

    public async Task DropSchemaAsync(string schemaName, CancellationToken ct = default)
    {
        ValidateSchemaName(schemaName);

        logger.LogWarning("Dropping tenant schema '{Schema}' — all data will be lost!", schemaName);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
        await cmd.ExecuteNonQueryAsync(ct);

        logger.LogInformation("Tenant schema '{Schema}' dropped", schemaName);
    }

    public async Task<bool> SchemaExistsAsync(string schemaName, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.schemata WHERE schema_name = @name)";
        cmd.Parameters.AddWithValue("name", schemaName);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static void ValidateSchemaName(string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name cannot be empty.", nameof(schemaName));

        // Only allow tenant_{guid} format to prevent SQL injection
        if (!schemaName.StartsWith("tenant_", StringComparison.Ordinal))
            throw new ArgumentException($"Schema name must start with 'tenant_'. Got: {schemaName}", nameof(schemaName));

        // Only alphanumeric, underscore, and hyphen allowed
        var suffix = schemaName["tenant_".Length..];
        if (!suffix.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
            throw new ArgumentException($"Schema name contains invalid characters: {schemaName}", nameof(schemaName));
    }
}
