using System.ComponentModel;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Infrastructure.Migrations;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Infrastructure.Jobs;

/// <summary>Parameters for the platform-wide migration-drift audit job.</summary>
public sealed record PlatformAuditMigrationDriftParams : JobParams;

/// <summary>
/// T-013 (ADR-0002 + ADR-0003): nightly audit that compares each tenant's
/// applied migration head (per module) against the platform assembly's
/// known head. Drift rows land in <c>platform_migration_drift</c> and
/// trigger a single aggregated <see cref="MigrationDriftDetectedIntegrationEvent"/>
/// at sweep end. Tenants whose migration started within the last
/// <see cref="SuppressionWindow"/> are excluded — that overlaps the
/// rolling-deploy migration window from
/// <c>docs/operations/migration-orchestration.md</c> §3.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Outbox vs. direct publish.</b> AC4 of T-013 specifies "via outbox",
/// but the platform-level outbox infrastructure (a <c>public</c>-schema
/// outbox table polled by <c>OutboxProcessor</c>) does not yet exist —
/// the existing outbox is per-tenant. Rather than expand T-013 into
/// platform-outbox plumbing, this job publishes via <see cref="IEventBus"/>
/// directly and accepts the trade-off: if the broker call drops, the
/// drift evidence still lives in <c>platform_migration_drift</c>
/// indefinitely (operators query it directly during ops triage). A
/// follow-up task should introduce a platform outbox so this can route
/// through the standard reliable-delivery path.
/// </para>
/// </remarks>
[Queue("maintenance")]
[DisplayName("platform:audit-migration-drift")]
public sealed class PlatformAuditMigrationDriftJob(
    ITenantContextAccessor tenantContextAccessor,
    PlatformDbContext platformDb,
    IEnumerable<IModuleMigration> moduleMigrations,
    MigrationDriftLogDbContext driftDb,
    IEventBus eventBus,
    ILogger<PlatformAuditMigrationDriftJob> logger,
    TimeProvider? timeProvider = null)
    : NexoraJob<PlatformAuditMigrationDriftParams>(tenantContextAccessor, logger)
{
    private const string RecurringJobId = "platform:audit-migration-drift";
    private const string PlatformSentinelTenantId = "platform";

    /// <summary>
    /// Window during which drift alerts are suppressed (rolling-deploy
    /// migration is expected to take less than this). Matches the value
    /// quoted in the migration-orchestration runbook §3.3.
    /// </summary>
    public static readonly TimeSpan SuppressionWindow = TimeSpan.FromHours(2);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Registers the recurring schedule on the supplied scheduler. Called
    /// from <c>IdentityModule.ConfigureJobs</c> at startup. Cron <c>0 5 * * *</c>
    /// — 05:00 UTC daily, well after the typical 03:00 UTC purge window
    /// so the drift audit reads a settled schema state.
    /// </summary>
    public static void RegisterRecurringSchedule(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<PlatformAuditMigrationDriftJob>(
            RecurringJobId,
            cronExpression: "0 5 * * *",
            methodCall: job => job.RunAsync(
                new PlatformAuditMigrationDriftParams { TenantId = PlatformSentinelTenantId },
                CancellationToken.None),
            queue: "maintenance");
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(PlatformAuditMigrationDriftParams parameters, CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var suppressionCutoff = nowUtc - SuppressionWindow;

        // Iterate all non-deleted, non-Terminated tenants. MigrationFailed
        // tenants ARE included — drift on them is meaningful evidence that
        // the failure left the schema mid-migration. Trial / Active /
        // Suspended all migrate normally.
        var tenants = await platformDb.Tenants
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.Status != TenantStatus.Terminated)
            .Select(t => new TenantSummary(t.Id.Value, t.LastMigrationStartedAtUtc))
            .ToListAsync(ct);

        var modulesList = moduleMigrations.ToList();
        var driftRows = new List<MigrationDrift>();
        var tenantsWithDrift = new HashSet<Guid>();

        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();

            // 2h suppression: if this tenant's migration started recently,
            // any divergence is expected and we skip recording. A tenant
            // that has NEVER migrated (LastMigrationStartedAtUtc == null)
            // is NOT suppressed — drift on a never-migrated tenant means
            // the assembly carries migrations the tenant has not seen yet,
            // which is exactly what the audit is built to catch.
            var inSuppressionWindow =
                tenant.LastMigrationStartedAtUtc.HasValue
                && tenant.LastMigrationStartedAtUtc.Value > suppressionCutoff;

            if (inSuppressionWindow)
            {
                logger.LogDebug(
                    "Drift audit: tenant {TenantId} is in the rolling-migration suppression window (started {StartedAt:O}); skipped.",
                    tenant.TenantId, tenant.LastMigrationStartedAtUtc!.Value);
                continue;
            }

            var schemaName = $"tenant_{tenant.TenantId}";

            foreach (var moduleMigration in modulesList)
            {
                MigrationHeadsReport report;
                try
                {
                    report = await moduleMigration.GetMigrationHeadsAsync(schemaName, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // CLAUDE.md "no catch(Exception)" exempts background-job
                    // boundaries — one module's reflection / DB hiccup must
                    // not abort the whole sweep.
                    logger.LogWarning(ex,
                        "Drift audit: GetMigrationHeadsAsync threw for tenant {TenantId} module {Module}; skipped.",
                        tenant.TenantId, moduleMigration.ModuleName);
                    continue;
                }

                if (!report.HasDrift) continue;

                driftRows.Add(MigrationDrift.Create(
                    tenant.TenantId, moduleMigration.ModuleName, report.KnownHead, report.AppliedHead));
                tenantsWithDrift.Add(tenant.TenantId);
            }
        }

        if (driftRows.Count == 0)
        {
            logger.LogInformation(
                "Drift audit completed: {TenantCount} tenants checked across {ModuleCount} modules, no drift detected.",
                tenants.Count, modulesList.Count);
            return;
        }

        await driftDb.Drifts.AddRangeAsync(driftRows, ct);
        await driftDb.SaveChangesAsync(ct);

        // Direct publish — see <remarks> on the class for why this bypasses
        // the per-tenant outbox.
        try
        {
            await eventBus.PublishAsync(new MigrationDriftDetectedIntegrationEvent
            {
                TenantId = PlatformSentinelTenantId,
                TenantCount = tenantsWithDrift.Count,
                DriftRowCount = driftRows.Count,
                AuditCompletedAtUtc = nowUtc,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Drift audit: failed to publish MigrationDriftDetectedIntegrationEvent — drift evidence still recorded in platform_migration_drift ({DriftRowCount} rows).",
                driftRows.Count);
        }

        logger.LogWarning(
            "Drift audit detected drift: {DriftRows} row(s) across {TenantsWithDrift} tenant(s) (out of {TotalTenants} checked).",
            driftRows.Count, tenantsWithDrift.Count, tenants.Count);
    }

    private readonly record struct TenantSummary(Guid TenantId, DateTime? LastMigrationStartedAtUtc);
}
