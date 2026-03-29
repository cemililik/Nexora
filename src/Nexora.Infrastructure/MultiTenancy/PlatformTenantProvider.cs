using Microsoft.Extensions.Configuration;
using Npgsql;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.MultiTenancy;

/// <summary>
/// Queries active tenants directly from the platform_tenants table in the public schema
/// using raw Npgsql. This avoids any dependency on tenant context or EF Core DbContext,
/// making it safe for cross-tenant background jobs and startup operations.
/// </summary>
public sealed class PlatformTenantProvider(IConfiguration configuration) : IActiveTenantProvider
{
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveTenantInfo>> GetActiveTenantsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT "Id"::text
            FROM public.platform_tenants
            WHERE "Status" IN ('Active', 'Trial')
              AND "IsDeleted" = false
            """;

        var tenants = new List<ActiveTenantInfo>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var tenantId = reader.GetString(0);
            tenants.Add(new ActiveTenantInfo(tenantId, $"tenant_{tenantId}"));
        }

        return tenants;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveTenantInfo>> GetActiveTenantsWithModuleAsync(
        string moduleName, CancellationToken ct = default)
    {
        const string sql = """
            SELECT t."Id"::text
            FROM public.platform_tenants t
            INNER JOIN public.platform_tenant_modules m
                ON t."Id" = m."TenantId"
            WHERE t."Status" IN ('Active', 'Trial')
              AND t."IsDeleted" = false
              AND m."IsActive" = true
              AND m."IsDeleted" = false
              AND m."ModuleName" = @module
            """;

        var tenants = new List<ActiveTenantInfo>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("module", moduleName);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var tenantId = reader.GetString(0);
            tenants.Add(new ActiveTenantInfo(tenantId, $"tenant_{tenantId}"));
        }

        return tenants;
    }
}
