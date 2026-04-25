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

    /// <summary>
    /// T-013: returns the migration heads (assembly-known + DB-applied) for
    /// drift detection. Default no-op implementation returns empty lists so
    /// Phase 1.5 modules that ship via <c>DevelopmentSeed.ApplySchemaUpdatesAsync</c>
    /// (no EF migrations yet) report "no drift possible". Phase 2 modules
    /// with EF migrations override this to return both lists from EF Core's
    /// <c>Database.GetMigrations()</c> + <c>Database.GetAppliedMigrationsAsync()</c>.
    /// </summary>
    Task<MigrationHeadsReport> GetMigrationHeadsAsync(string schemaName, CancellationToken ct = default)
        => Task.FromResult(MigrationHeadsReport.Empty);
}

/// <summary>
/// Snapshot used by <c>PlatformAuditMigrationDriftJob</c> (T-013) to compare
/// what an assembly compiled-in vs. what is actually applied to a tenant
/// schema. Drift exists when the lists' last entries diverge — i.e. the
/// tenant has not been migrated to the assembly's head, or the tenant has
/// been migrated past it (rollback / out-of-band hotfix).
/// </summary>
/// <param name="Known">
/// Migration IDs the module's assembly carries — <c>DbContext.Database.GetMigrations()</c>.
/// Ordered chronologically; the last entry is the assembly head.
/// </param>
/// <param name="Applied">
/// Migration IDs actually present in the tenant schema's
/// <c>__EFMigrationsHistory</c> — <c>DbContext.Database.GetAppliedMigrationsAsync(ct)</c>.
/// Ordered chronologically; the last entry is the schema head.
/// </param>
public sealed record MigrationHeadsReport(
    IReadOnlyList<string> Known,
    IReadOnlyList<string> Applied)
{
    /// <summary>Empty result — module has no EF migrations yet, or schema is brand-new.</summary>
    public static readonly MigrationHeadsReport Empty = new([], []);

    /// <summary>The last item in <see cref="Known"/>, or <see langword="null"/> when empty.</summary>
    public string? KnownHead => Known.Count == 0 ? null : Known[^1];

    /// <summary>The last item in <see cref="Applied"/>, or <see langword="null"/> when empty.</summary>
    public string? AppliedHead => Applied.Count == 0 ? null : Applied[^1];

    /// <summary>True when the schema head differs from the assembly head.</summary>
    public bool HasDrift => KnownHead != AppliedHead;
}
