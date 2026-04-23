using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Infrastructure.Jobs;

/// <summary>Parameters for the audit partition maintenance job.</summary>
public sealed record AuditPartitionMaintenanceJobParams : JobParams
{
    /// <summary>How many future monthly partitions to keep pre-created (including the current month).</summary>
    public int LookaheadMonths { get; init; } = 3;
}

/// <summary>
/// Monthly recurring job that ensures a range partition exists on <c>audit_entries</c> for the
/// current month and the next <see cref="AuditPartitionMaintenanceJobParams.LookaheadMonths"/>
/// months. Runs as a no-op when the table is not partitioned — conversion is a one-time
/// migration performed outside this job (see <c>DevelopmentSeed.EnsureAuditPartitioningAsync</c>
/// in dev or the production runbook).
/// </summary>
/// <remarks>
/// Each partition is named <c>audit_entries_yYYYY_mMM</c> and covers <c>[first-of-month, first-of-next-month)</c>.
/// All statements use <c>IF NOT EXISTS</c> so the job is fully idempotent.
/// </remarks>
public sealed class AuditPartitionMaintenanceJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditPartitionMaintenanceJob> logger) : PlatformJob<AuditPartitionMaintenanceJobParams>(tenantProvider, scopeFactory, logger)
{
    /// <inheritdoc />
    protected override string? GetRequiredModule() => "audit";

    /// <inheritdoc />
    protected override async Task ExecuteForTenantAsync(
        AuditPartitionMaintenanceJobParams parameters,
        ActiveTenantInfo tenant,
        IServiceProvider scopedServices,
        CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<AuditDbContext>();
        var schema = $"tenant_{tenant.TenantId}";

        if (!await IsPartitionedAsync(dbContext, schema, ct))
        {
            logger.LogInformation(
                "Audit partition maintenance skipped for tenant {TenantId}: table is not partitioned",
                tenant.TenantId);
            return;
        }

        var today = DateTime.UtcNow.Date;
        var monthAnchor = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < parameters.LookaheadMonths; i++)
        {
            var start = monthAnchor.AddMonths(i);
            var end = start.AddMonths(1);
            var partitionName = $"audit_entries_y{start:yyyy}_m{start:MM}";

            var sql = string.Format(
                CultureInfo.InvariantCulture,
                """
                CREATE TABLE IF NOT EXISTS "{0}"."{1}"
                PARTITION OF "{0}"."audit_entries"
                FOR VALUES FROM ('{2:yyyy-MM-dd}') TO ('{3:yyyy-MM-dd}')
                """,
                schema, partitionName, start, end);

            await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            logger.LogInformation(
                "Audit partition ensured: tenant={TenantId} partition={PartitionName} range=[{Start:yyyy-MM-dd}..{End:yyyy-MM-dd})",
                tenant.TenantId, partitionName, start, end);
        }
    }

    private static async Task<bool> IsPartitionedAsync(AuditDbContext db, string schema, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM pg_partitioned_table pt
                    JOIN pg_class c ON c.oid = pt.partrelid
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = @schema AND c.relname = 'audit_entries')
                """;
            var param = cmd.CreateParameter();
            param.ParameterName = "@schema";
            param.Value = schema;
            cmd.Parameters.Add(param);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is bool b && b;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
