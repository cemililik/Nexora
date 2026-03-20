using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure;

/// <summary>
/// Applies Documents module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class DocumentsModuleMigration(
    IServiceProvider serviceProvider,
    ILogger<DocumentsModuleMigration> logger) : IModuleMigration
{
    public string ModuleName => "documents";

    /// <summary>Applies EF Core migrations for the Documents module schema.</summary>
    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        logger.LogInformation("Starting Documents module migration for schema {SchemaName}", schemaName);

        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new DocumentsDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);

        logger.LogInformation("Completed Documents module migration for schema {SchemaName}", schemaName);
    }

    /// <summary>Seeds initial data for the Documents module schema.</summary>
    public Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        // Documents module permissions are seeded by Identity module's permission system.
        // No additional seed data needed at this time.
        return Task.CompletedTask;
    }

    private DbContextOptions<DocumentsDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
