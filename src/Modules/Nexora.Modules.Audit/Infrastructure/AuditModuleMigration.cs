using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Infrastructure;

/// <summary>
/// Module migration for tenant provisioning — creates audit tables.
/// </summary>
public sealed class AuditModuleMigration(IServiceProvider serviceProvider) : IModuleMigration
{
    public string ModuleName => "audit";

    /// <inheritdoc />
    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new AuditDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);
    }

    /// <inheritdoc />
    public Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        // Audit module permissions are seeded by Identity module's permission system.
        return Task.CompletedTask;
    }

    private DbContextOptions<AuditDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
