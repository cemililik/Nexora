using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure;

/// <summary>
/// Applies Contacts module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class ContactsModuleMigration(IServiceProvider serviceProvider) : IModuleMigration
{
    private const string HardDeleteFlagKey = "gdpr.hard_delete.enabled";

    /// <inheritdoc />
    public string ModuleName => "contacts";

    /// <inheritdoc />
    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new ContactsDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);
    }

    /// <inheritdoc />
    public async Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        // Contacts module permissions are seeded by Identity module's permission system.
        // We only seed platform defaults that are specific to Contacts compliance flags.
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        // Single-argument SetTenant is intentional and correct per T-018 /
        // ITenantConfiguration contract: the config store is tenant-scoped and does
        // NOT require an organization id. The seed runs per-tenant (not per-org), so
        // there is no meaningful org at this stage of startup anyway.
        accessor.SetTenant(ExtractTenantId(schemaName));

        // Hard fail if ITenantConfiguration is not registered — production misconfiguration
        // should surface immediately, not silently skip seeding the hard-delete flag.
        var tenantConfig = scope.ServiceProvider.GetRequiredService<ITenantConfiguration>();

        // GDPR Article 17 hard-delete is opt-in per tenant. Default: anonymize mode.
        // Idempotent: GetAsync returns default (false) if the key has never been set,
        // which is indistinguishable from an explicit false — so we only write if the
        // row is genuinely missing to avoid clobbering an operator override.
        var current = await tenantConfig.GetAsync<bool?>(HardDeleteFlagKey, ct);
        if (current is null)
            await tenantConfig.SetAsync(HardDeleteFlagKey, false, ct);
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

    /// <summary>
    /// Strips the <c>tenant_</c> prefix from a schema name and validates the
    /// remainder is a parseable GUID. Throws when either invariant is violated
    /// — silently passing a malformed string into <c>ITenantContextAccessor.SetTenant</c>
    /// surfaces later as obscure model-cache mismatches or "tenant context not set"
    /// errors at the first DbContext query, exactly the failure mode T-018 was
    /// filed to prevent.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="schemaName"/> does not start with <c>tenant_</c>
    /// or when the suffix does not parse as a GUID. The message includes the
    /// rejected value and the expected format so operator diagnostics are clear.
    /// </exception>
    private static string ExtractTenantId(string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName) ||
            !schemaName.StartsWith("tenant_", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid schema name '{schemaName}'. Expected format: 'tenant_<guid>'.",
                nameof(schemaName));
        }

        var suffix = schemaName["tenant_".Length..];
        if (!Guid.TryParse(suffix, out var guid))
        {
            throw new ArgumentException(
                $"Invalid schema name '{schemaName}'. Suffix '{suffix}' is not a parseable GUID.",
                nameof(schemaName));
        }

        return guid.ToString();
    }
}
