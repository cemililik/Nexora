namespace Nexora.Infrastructure.Migrations;

/// <summary>
/// Forensic record written to the platform-scoped
/// <c>platform_migration_failures</c> table whenever
/// <see cref="MigrationRunner"/> aborts a tenant migration. Operators
/// triage by querying this table (see
/// <c>docs/operations/migration-orchestration.md</c> §2.3); the
/// <see cref="Tenant"/> aggregate's <c>MigrationFailed</c> state quarantines
/// requests at the API layer, but the why-did-it-fail evidence lives here.
/// </summary>
public sealed class MigrationFailure
{
    /// <summary>Surrogate primary key — tenant migrations can fail more than once across deploys.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant whose migration failed. Plain Guid to match the platform-wide convention (see DemoSeedMarker).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Module name (<c>IModule.Name</c>) whose <c>MigrateAsync</c> threw.</summary>
    public required string ModuleName { get; init; }

    /// <summary>Exception type name (<c>ex.GetType().FullName</c>) — distinguishes Npgsql vs. EF vs. unknown.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Exception message — may include SQL fragments. Kept verbatim; redaction is the operator's responsibility when sharing.</summary>
    public required string ExceptionMessage { get; init; }

    /// <summary>Stack trace at the point of failure. Truncated to 8000 chars in the column to bound row size.</summary>
    public string? StackTrace { get; init; }

    /// <summary>UTC timestamp of the failure. Set at row creation.</summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Factory matching the orchestrator's call shape — keeps the
    /// "always set OccurredAtUtc + Id" invariant in one place.
    /// </summary>
    public static MigrationFailure Create(
        Guid tenantId,
        string moduleName,
        Exception exception)
    {
        return new MigrationFailure
        {
            TenantId = tenantId,
            ModuleName = moduleName,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            ExceptionMessage = exception.Message,
            StackTrace = Truncate(exception.StackTrace, 8000),
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
