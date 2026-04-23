using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Reporting.Api;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.Modules.Reporting.Application.Services;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Jobs;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;

namespace Nexora.Modules.Reporting;

/// <summary>Reporting engine module providing SQL-based reports, dashboards, and scheduled exports.</summary>
public sealed class ReportingModule : IModule
{
    public string Name => "reporting";
    public string DisplayName => "lockey_reporting_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["identity"];

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReportingDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "reporting");
            });
            options.AddNexoraAuditInterceptor(sp);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ReportingModule).Assembly));

        services.AddSingleton<IModuleMigration, ReportingModuleMigration>();

        services.AddSingleton<ISqlQueryValidator, SqlQueryValidator>();
        services.AddScoped<IReportExecutionService, ReportExecutionService>();
        services.AddScoped<ReportExportService>();

        // Outbox for transactional event publishing
        services.AddScoped<IOutbox, OutboxService<ReportingDbContext>>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // No cross-module integration events consumed at this time.
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReportDefinitionEndpoints();
        endpoints.MapReportExecutionEndpoints();
        endpoints.MapReportScheduleEndpoints();
        endpoints.MapDashboardEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<ScheduledReportDispatcherJob>(
            "reporting:scheduled-report-dispatch",
            "*/15 * * * *", // Every 15 minutes
            job => job.RunAsync(new ScheduledReportDispatcherJobParams { TenantId = "system" }, CancellationToken.None),
            "default");
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct)
    {
        registry.Register("reporting", "definition", "read",   "lockey_reporting_permission_definition_read");
        registry.Register("reporting", "definition", "manage", "lockey_reporting_permission_definition_manage");
        registry.Register("reporting", "execution", "run",  "lockey_reporting_permission_execution_run");
        registry.Register("reporting", "execution", "read", "lockey_reporting_permission_execution_read");
        registry.Register("reporting", "schedule", "manage", "lockey_reporting_permission_schedule_manage");
        registry.Register("reporting", "dashboard", "read",   "lockey_reporting_permission_dashboard_read");
        registry.Register("reporting", "dashboard", "manage", "lockey_reporting_permission_dashboard_manage");
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
