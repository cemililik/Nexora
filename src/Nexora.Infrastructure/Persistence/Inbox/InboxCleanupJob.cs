using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Persistence.Inbox;

/// <summary>Parameters for the inbox cleanup job.</summary>
public sealed record InboxCleanupJobParams : JobParams
{
    /// <summary>Number of days to retain processed inbox messages before deletion.</summary>
    public int RetentionDays { get; init; } = 30;
}

/// <summary>
/// Platform-level recurring job that deletes old inbox messages across all tenant schemas.
/// Inbox messages are stored per-tenant (in tenant_* schemas) and are used for idempotent
/// integration event consumption. Once past the retention period, they can be safely removed
/// since the deduplication window has long expired.
/// </summary>
public sealed class InboxCleanupJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<InboxCleanupJob> logger) : PlatformJob<InboxCleanupJobParams>(tenantProvider, scopeFactory, logger)
{
    /// <inheritdoc />
    protected override async Task ExecuteForTenantAsync(
        InboxCleanupJobParams parameters,
        ActiveTenantInfo tenant,
        IServiceProvider scopedServices,
        CancellationToken ct)
    {
        var configuration = scopedServices.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        var cutoff = DateTimeOffset.UtcNow.AddDays(-parameters.RetentionDays);

        // Use raw SQL because inbox_messages lives in the tenant schema and is shared across
        // modules within that schema. There is no dedicated InboxDbContext.
        var sql = $"""
            DELETE FROM "{tenant.SchemaName}".inbox_messages
            WHERE "ProcessedAt" < @cutoff
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("cutoff", cutoff);

        var deletedCount = await command.ExecuteNonQueryAsync(ct);

        if (deletedCount > 0)
        {
            logger.LogInformation(
                "InboxCleanupJob deleted {DeletedCount} inbox messages older than {RetentionDays} days for tenant {TenantId}",
                deletedCount, parameters.RetentionDays, tenant.TenantId);
        }
    }
}
