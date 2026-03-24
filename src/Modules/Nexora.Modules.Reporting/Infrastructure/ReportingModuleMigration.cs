using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Infrastructure;

/// <summary>
/// Applies Reporting module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class ReportingModuleMigration(IServiceProvider serviceProvider) : IModuleMigration
{
    public string ModuleName => "reporting";

    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new ReportingDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);
    }

    public Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        // Reporting permissions are seeded centrally in IdentityModuleMigration.SeedAsync().
        return Task.CompletedTask;
    }

    private DbContextOptions<ReportingDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
