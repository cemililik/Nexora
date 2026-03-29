using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Audit.Api;
using Nexora.Modules.Audit.Application.Services;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Audit.Infrastructure.Stores;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

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
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AuditModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, AuditModuleMigration>();

        // Register audit infrastructure services
        services.AddScoped<IAuditStore, PostgresAuditStore>();
        services.AddScoped<IAuditConfigService, AuditConfigService>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Audit module does not consume integration events from other modules.
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuditLogEndpoints();
        endpoints.MapAuditSettingsEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        // No recurring jobs in initial version.
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
