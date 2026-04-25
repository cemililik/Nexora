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

    /// <summary>
    /// Exception message — may include SQL fragments, external system
    /// identifiers, or other operational data. Kept verbatim because the
    /// row's purpose is post-mortem forensics. <b>Redaction is the
    /// operator's responsibility when sharing</b>: sanitize before
    /// pasting into ticket trackers, screen-share, or chat. Refer to the
    /// operations runbook for the redaction checklist (PII, secrets,
    /// privileged identifiers).
    /// </summary>
    public required string ExceptionMessage { get; init; }

    /// <summary>
    /// Stack trace at the point of failure. Truncated to <see cref="StackTraceMaxLength"/>
    /// chars to bound row size while staying large enough to contain the
    /// full async-method-chain + EF Core query pipeline + MediatR
    /// behaviour stack that real production exceptions surface.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Cap for <see cref="StackTrace"/>. Selected to fit the typical .NET
    /// 9 async + EF Core + MediatR chain (≈10–15 KB observed across the
    /// suite) with headroom; see review #21.
    /// </summary>
    public const int StackTraceMaxLength = 16000;

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
            StackTrace = Truncate(exception.StackTrace, StackTraceMaxLength),
        };
    }

    /// <summary>
    /// Internal so the architecture-level test can call it directly with
    /// a synthetic over-cap input (real exceptions rarely have stacks
    /// long enough to exercise the truncation branch).
    /// </summary>
    internal static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
