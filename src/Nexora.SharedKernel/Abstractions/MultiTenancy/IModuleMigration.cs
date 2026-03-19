namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>
/// Implemented by each module to apply its database schema to a tenant.
/// Registered during module ConfigureServices and invoked during tenant provisioning.
/// </summary>
public interface IModuleMigration
{
    /// <summary>Module name this migration belongs to.</summary>
    string ModuleName { get; }

    /// <summary>Applies the module's EF migrations to the given schema.</summary>
    Task MigrateAsync(string schemaName, CancellationToken ct = default);

    /// <summary>Seeds initial data for the module in the given schema.</summary>
    Task SeedAsync(string schemaName, CancellationToken ct = default);
}
