using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Identity.Api;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "identity";
    public string DisplayName => "Identity & Access Management";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IdentityModule).Assembly));
    }

    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Identity module is the event source, not consumer — no handlers to register yet
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrganizationEndpoints();
    }

    public void ConfigureJobs(IJobScheduler scheduler)
    {
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    public Task OnStartupAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task OnInstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        throw new DomainException("lockey_identity_error_cannot_uninstall");
    }
}
