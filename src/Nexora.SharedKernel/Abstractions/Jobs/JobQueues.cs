namespace Nexora.SharedKernel.Abstractions.Jobs;

/// <summary>
/// Standard Hangfire queue names. Jobs must use one of these queues.
/// </summary>
public static class JobQueues
{
    /// <summary>Payment processing, critical notifications</summary>
    public const string Critical = "critical";

    /// <summary>Normal priority jobs</summary>
    public const string Default = "default";

    /// <summary>Mass operations, bulk imports/exports</summary>
    public const string Bulk = "bulk";

    /// <summary>Cleanup, archival, maintenance tasks</summary>
    public const string Maintenance = "maintenance";

    /// <summary>All available queue names.</summary>
    public static readonly string[] All = [Critical, Default, Bulk, Maintenance];
}
