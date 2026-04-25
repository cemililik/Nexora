using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.FeatureFlags;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Identity.Api;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Authorization;
using Nexora.Modules.Identity.Infrastructure.IntegrationEvents;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.FeatureFlags;
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

        // Register module migration for tenant provisioning. Register as both the
        // interface (for tenant-provisioning iteration) and the concrete type (so
        // DevelopmentSeed can resolve the Identity migration directly to delegate
        // permission + role seeding).
        services.AddSingleton<IdentityModuleMigration>();
        services.AddSingleton<IModuleMigration>(sp => sp.GetRequiredService<IdentityModuleMigration>());

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

        // T-016: feature-flag service backed by tenant config. The
        // optional LaunchDarkly backend swaps in here at deploy time
        // when a SaaS deployment configures the integration.
        services.AddScoped<IFeatureFlagService, TenantConfigFeatureFlagService>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Self-consuming handler for cross-instance permission cache invalidation
        services.AddScoped<IIntegrationEventHandler<UserRolesChangedIntegrationEvent>,
            UserRolesChangedEventHandler>();

        // Cross-module: GDPR erasure in Contacts cascades to unlink users.
        services.AddScoped<IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>,
            ContactGdprDeletedIntegrationEventHandler>();
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
        endpoints.MapDemoScenarioEndpoints();
        endpoints.MapFeatureFlagEndpoints(); // T-016
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        // T-025: drops module tables that aged past the uninstall retention
        // window (default 30 days, ADR-0028). Outer cron fires platform-wide
        // at 03:00 UTC; inside it fans per-tenant child runs out with
        // deterministic 0–119 minute jitter so DROP TABLEs don't all hit
        // Postgres WAL writers at the same instant.
        Infrastructure.Jobs.PurgeUninstalledModulesJob.RegisterRecurringSchedule(scheduler);

        // T-013: nightly drift audit comparing each tenant's applied
        // migration head vs the platform assembly's known head. 05:00 UTC
        // sits well after the 03:00 purge so the schema is settled.
        Infrastructure.Jobs.PlatformAuditMigrationDriftJob.RegisterRecurringSchedule(scheduler);

        // T-015: daily fetch of the signed license-revocation bundle
        // (03:00 UTC). Lives under Nexora.Infrastructure.Licensing —
        // registered here because IdentityModule is the single
        // platform-level scheduler entry point.
        Nexora.Infrastructure.Licensing.RevocationListFetchJob.RegisterRecurringSchedule(scheduler);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct)
    {
        // Identity & cross-platform permissions (ADR-004 / permissions.md §3).
        // The identity.tenants.* block is Platform-scope — tenant admins cannot hold these.
        registry.Register("identity", "tenants", "read",   "lockey_identity_permission_tenants_read",   PermissionScope.Platform);
        registry.Register("identity", "tenants", "manage", "lockey_identity_permission_tenants_manage", PermissionScope.Platform);

        // Organizations, users, roles — tenant-scope.
        registry.Register("identity", "organizations", "read",   "lockey_identity_permission_organizations_read");
        registry.Register("identity", "organizations", "create", "lockey_identity_permission_organizations_create");
        registry.Register("identity", "organizations", "update", "lockey_identity_permission_organizations_update");
        registry.Register("identity", "organizations", "delete", "lockey_identity_permission_organizations_delete");
        registry.Register("identity", "users", "read",         "lockey_identity_permission_users_read");
        registry.Register("identity", "users", "create",       "lockey_identity_permission_users_create");
        registry.Register("identity", "users", "update",       "lockey_identity_permission_users_update");
        registry.Register("identity", "users", "delete",       "lockey_identity_permission_users_delete");
        registry.Register("identity", "users", "link_contact", "lockey_identity_permission_users_link_contact");
        registry.Register("identity", "roles", "read",   "lockey_identity_permission_roles_read");
        registry.Register("identity", "roles", "create", "lockey_identity_permission_roles_create");
        registry.Register("identity", "roles", "update", "lockey_identity_permission_roles_update");
        registry.Register("identity", "roles", "delete", "lockey_identity_permission_roles_delete");
        registry.Register("identity", "modules", "read",   "lockey_identity_permission_modules_read");
        registry.Register("identity", "modules", "manage", "lockey_identity_permission_modules_manage");

        // T-016: feature-flag toggles. Platform-scope so only Platform Admin
        // can flip a flag — never tenant admins (preserves the rollout
        // contract: flags are operator-controlled progressive-delivery
        // levers, not tenant settings).
        registry.Register("identity", "feature_flags", "manage", "lockey_identity_permission_feature_flags_manage", PermissionScope.Platform);

        // Platform-wide operator permission (reserved for NMP operators — ADR-0025).
        registry.Register("platform", "compliance", "policy_manage",
            "lockey_platform_permission_compliance_policy_manage", PermissionScope.Platform);

        // T-008: Platform operators that can push demo content into an existing
        // tenant schema. Platform-scope on purpose — tenant admins must not be
        // able to seed demo data into their own tenant from the browser.
        registry.Register("platform", "tenants", "create_demo",
            "lockey_identity_permission_tenants_create_demo", PermissionScope.Platform);

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
