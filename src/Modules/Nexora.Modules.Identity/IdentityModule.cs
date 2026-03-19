using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Identity.Api;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "identity";
    public string DisplayName => "lockey_identity_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Tenant-scoped DbContext (per-request, resolves tenant schema)
        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
        });

        // Platform-level DbContext (public schema — tenant management)
        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IdentityModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, IdentityModuleMigration>();

        // Keycloak Admin API
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>((sp, client) =>
        {
            var kcOptions = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()!;
            client.BaseAddress = new Uri(kcOptions.BaseUrl);
        });
    }

    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Identity module is the event source, not consumer — no handlers to register yet
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrganizationEndpoints();
        endpoints.MapTenantEndpoints();
        endpoints.MapUserEndpoints();
        endpoints.MapRoleEndpoints();
        endpoints.MapAuditEndpoints();
        endpoints.MapModuleEndpoints();
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
