using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure;

/// <summary>
/// Module migration for tenant provisioning — creates notification tables and seeds permissions.
/// </summary>
public sealed class NotificationsModuleMigration(IServiceProvider serviceProvider) : IModuleMigration
{
    public string ModuleName => "notifications";

    /// <inheritdoc />
    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new NotificationsDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);
    }

    /// <inheritdoc />
    public Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        // Notification module permissions are seeded by Identity module's permission system.
        // Default notification templates will be seeded in Batch 2.
        return Task.CompletedTask;
    }

    private DbContextOptions<NotificationsDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
