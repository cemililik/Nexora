using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Identity.Api;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Authorization;
using Nexora.Modules.Identity.Infrastructure.IntegrationEvents;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity;

/// <summary>Identity module providing authentication, tenants, organizations, users, and RBAC.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "identity";
    public string DisplayName => "lockey_identity_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    /// <inheritdoc />
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
            options.AddNexoraAuditInterceptor(sp);
        });

        // Platform-level DbContext (public schema — tenant management)
        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IdentityModule).Assembly));

        // Locale context — resolves language/currency/timezone per request (User → Tenant → Platform defaults)
        services.AddScoped<ILocaleContext, LocaleContextResolver>();

        // Permission-based authorization — loads user permissions from Identity DB
        services.AddScoped<IUserPermissionService, UserPermissionService>();

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, IdentityModuleMigration>();

        // Keycloak Admin API
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>((sp, client) =>
        {
            var kcOptions = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()!;
            client.BaseAddress = new Uri(kcOptions.BaseUrl);
        });

        // Register inbox guard for idempotent integration event consumption
        services.AddScoped<IInboxGuard, InboxGuard<IdentityDbContext>>();
        services.AddScoped<IOutbox, OutboxService<IdentityDbContext>>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Self-consuming handler for cross-instance permission cache invalidation
        services.AddScoped<IIntegrationEventHandler<UserRolesChangedIntegrationEvent>,
            UserRolesChangedEventHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrganizationEndpoints();
        endpoints.MapTenantEndpoints();
        endpoints.MapUserEndpoints();
        endpoints.MapRoleEndpoints();
        endpoints.MapAuditEndpoints();
        endpoints.MapModuleEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
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
        throw new DomainException("lockey_identity_error_cannot_uninstall");
    }
}
