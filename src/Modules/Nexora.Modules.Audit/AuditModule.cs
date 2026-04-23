using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Modules.Audit.Api;
using Nexora.Modules.Audit.Application.Services;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Audit.Infrastructure.IntegrationEvents;
using Nexora.Modules.Audit.Infrastructure.Jobs;
using Nexora.Modules.Audit.Infrastructure.Repositories;
using Nexora.Modules.Audit.Infrastructure.Stores;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Audit;

/// <summary>Module entry point for the Audit module, handling registration, endpoints, and services.</summary>
public sealed class AuditModule : IModule
{
    public string Name => "audit";
    public string DisplayName => "lockey_audit_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["identity"];

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
            });
            options.AddNexoraAuditInterceptor(sp);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AuditModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, AuditModuleMigration>();

        // Register repositories
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IAuditSettingRepository, AuditSettingRepository>();

        // Register audit infrastructure services
        services.AddScoped<IAuditStore, PostgresAuditStore>();
        services.AddScoped<IAuditConfigService, AuditConfigService>();

        // Register inbox guard for idempotent integration event consumption
        services.AddScoped<IInboxGuard, InboxGuard<AuditDbContext>>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // GDPR erasure propagation — redacts PII payloads and appends a compliance record.
        services.AddScoped<IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>,
            ContactGdprDeletedIntegrationEventHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuditLogEndpoints();
        endpoints.MapAuditSettingsEndpoints();
        endpoints.MapAuthEventEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        // Weekly cleanup: prune audit entries older than per-module retention (Sunday 04:00 UTC).
        scheduler.AddOrUpdate<AuditCleanupJob>(
            "audit:cleanup-expired-entries",
            "0 4 * * 0",
            job => job.RunAsync(new AuditCleanupJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Maintenance);

        // Monthly partition creator: ensures next month's audit_entries partition exists (runs on day 20).
        scheduler.AddOrUpdate<AuditPartitionMaintenanceJob>(
            "audit:ensure-future-partition",
            "0 2 20 * *",
            job => job.RunAsync(new AuditPartitionMaintenanceJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Maintenance);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnInstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
