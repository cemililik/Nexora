using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Reporting.Api;
using Nexora.Modules.Reporting.Application.Services;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Jobs;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting;

/// <summary>Reporting engine module providing SQL-based reports, dashboards, and scheduled exports.</summary>
public sealed class ReportingModule : IModule
{
    public string Name => "reporting";
    public string DisplayName => "lockey_reporting_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["identity"];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReportingDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "reporting");
            });
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ReportingModule).Assembly));

        services.AddSingleton<IModuleMigration, ReportingModuleMigration>();

        services.AddSingleton<ISqlQueryValidator, SqlQueryValidator>();
        services.AddScoped<IReportExecutionService, ReportExecutionService>();
        services.AddScoped<ReportExportService>();
    }

    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // No cross-module integration events consumed at this time.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReportDefinitionEndpoints();
        endpoints.MapReportExecutionEndpoints();
        endpoints.MapReportScheduleEndpoints();
        endpoints.MapDashboardEndpoints();
    }

    public void ConfigureJobs(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<ScheduledReportDispatcherJob>(
            "reporting:scheduled-report-dispatch",
            "*/15 * * * *", // Every 15 minutes
            job => job.RunAsync(new ScheduledReportDispatcherJobParams { TenantId = "system" }, CancellationToken.None),
            "default");
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    public Task OnStartupAsync(CancellationToken ct)
    {
        // Reporting permissions are seeded centrally in IdentityModuleMigration.SeedAsync().
        return Task.CompletedTask;
    }

    public Task OnInstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
