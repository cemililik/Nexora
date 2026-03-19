using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure;

/// <summary>
/// Applies Contacts module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class ContactsModuleMigration(IServiceProvider serviceProvider) : IModuleMigration
{
    public string ModuleName => "contacts";

    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new ContactsDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);
    }

    public Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        // Contacts module permissions are seeded by Identity module's permission system.
        // No additional seed data needed at this time.
        return Task.CompletedTask;
    }

    private DbContextOptions<ContactsDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<ContactsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
