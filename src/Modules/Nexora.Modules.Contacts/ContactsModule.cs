using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Contacts.Api;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts;

public sealed class ContactsModule : IModule
{
    public string Name => "contacts";
    public string DisplayName => "lockey_contacts_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["identity"];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ContactsDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "contacts");
            });
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ContactsModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, ContactsModuleMigration>();

        // Register cross-module query service
        services.AddScoped<IContactQueryService, ContactQueryService>();

        // Register domain services
        services.AddScoped<Domain.Services.DuplicateDetectionService>();

        // Register activity contributor aggregator for 360-degree view
        services.AddScoped<ContactActivityContributorAggregator>();
    }

    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Domain event handlers are auto-registered via MediatR assembly scanning.
        // Integration event handlers for cross-module events:
        services.AddScoped<IIntegrationEventHandler<UserCreatedIntegrationEvent>,
            UserCreatedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>,
            OrganizationCreatedIntegrationEventHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var module = endpoints.MapGroup("/api/v1/contacts");

        module.MapContactEndpoints();
        module.MapTagEndpoints();
        module.MapContactAddressEndpoints();
        module.MapContactRelationshipEndpoints();
        module.MapCommunicationPreferenceEndpoints();
        module.MapContactNoteEndpoints();
        module.MapConsentEndpoints();
        module.MapContactActivityEndpoints();
        module.MapCustomFieldEndpoints();
        module.MapDuplicateEndpoints();
        module.MapImportExportEndpoints();
        module.MapGdprEndpoints();
    }

    public void ConfigureJobs(IJobScheduler scheduler)
    {
        // Import/export jobs are triggered on-demand via commands, not scheduled.
        // Recurring jobs (e.g., stale import cleanup) can be registered here:
        // scheduler.AddRecurring<ContactImportCleanupJob>("contacts:import-cleanup", "0 3 * * *", "maintenance");
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    public Task OnStartupAsync(CancellationToken ct)
    {
        // TODO: Register Contacts module permissions (e.g., contacts.contact.read, contacts.contact.write,
        // contacts.contact.delete, contacts.tag.manage, contacts.import.execute, contacts.gdpr.manage)
        // once IPermissionRegistrar is available in SharedKernel. Currently no modules implement
        // permission registration — this is a platform-wide gap to address.
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
