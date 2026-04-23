using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Contacts.Api;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts;

/// <summary>Contacts module providing unified contact registry, tagging, and 360-degree view.</summary>
public sealed class ContactsModule : IModule
{
    /// <inheritdoc />
    public string Name => "contacts";
    /// <inheritdoc />
    public string DisplayName => "lockey_contacts_module_display_name";
    /// <inheritdoc />
    public string Version => "1.0.0";
    /// <inheritdoc />
    public IReadOnlyList<string> Dependencies => ["identity"];

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ContactsDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "contacts");
            });
            options.AddNexoraAuditInterceptor(sp);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ContactsModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, ContactsModuleMigration>();

        // Register cross-module query service
        services.AddScoped<IContactQueryService, ContactQueryService>();

        // Register domain services
        services.AddScoped<Domain.Services.DuplicateDetectionService>();
        services.AddScoped<Domain.Services.IContactDuplicateMatcher,
            Infrastructure.Services.ContactDuplicateMatcher>();

        // Register activity contributor aggregator for 360-degree view
        services.AddScoped<ContactActivityContributorAggregator>();

        // Register inbox guard for idempotent integration event consumption
        services.AddScoped<IInboxGuard, InboxGuard<ContactsDbContext>>();
        // Register outbox for transactional event publishing (atomicity with business data)
        services.AddScoped<IOutbox, OutboxService<ContactsDbContext>>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Domain event handlers are auto-registered via MediatR assembly scanning.
        // Integration event handlers for cross-module events:
        services.AddScoped<IIntegrationEventHandler<UserCreatedIntegrationEvent>,
            UserCreatedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>,
            OrganizationCreatedIntegrationEventHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapContactEndpoints();
        endpoints.MapTagEndpoints();
        endpoints.MapContactAddressEndpoints();
        endpoints.MapContactRelationshipEndpoints();
        endpoints.MapCommunicationPreferenceEndpoints();
        endpoints.MapContactNoteEndpoints();
        endpoints.MapConsentEndpoints();
        endpoints.MapContactActivityEndpoints();
        endpoints.MapCustomFieldEndpoints();
        endpoints.MapDuplicateEndpoints();
        endpoints.MapImportExportEndpoints();
        endpoints.MapGdprEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        // Import/export jobs are triggered on-demand via commands, not scheduled.
        // Recurring jobs (e.g., stale import cleanup) can be registered here:
        // scheduler.AddRecurring<ContactImportCleanupJob>("contacts:import-cleanup", "0 3 * * *", "maintenance");
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct)
    {
        // Contacts module permissions (ADR-004 / permissions.md §3). All Tenant-scope.
        registry.Register("contacts", "contact", "read",   "lockey_contacts_permission_contact_read");
        registry.Register("contacts", "contact", "create", "lockey_contacts_permission_contact_create");
        registry.Register("contacts", "contact", "update", "lockey_contacts_permission_contact_update");
        registry.Register("contacts", "contact", "delete", "lockey_contacts_permission_contact_delete");
        registry.Register("contacts", "tag", "read",   "lockey_contacts_permission_tag_read");
        registry.Register("contacts", "tag", "create", "lockey_contacts_permission_tag_create");
        registry.Register("contacts", "tag", "update", "lockey_contacts_permission_tag_update");
        registry.Register("contacts", "tag", "delete", "lockey_contacts_permission_tag_delete");
        registry.Register("contacts", "custom-field", "read",   "lockey_contacts_permission_custom_field_read");
        registry.Register("contacts", "custom-field", "manage", "lockey_contacts_permission_custom_field_manage");
        registry.Register("contacts", "note", "read",   "lockey_contacts_permission_note_read");
        registry.Register("contacts", "note", "create", "lockey_contacts_permission_note_create");
        registry.Register("contacts", "note", "update", "lockey_contacts_permission_note_update");
        registry.Register("contacts", "note", "delete", "lockey_contacts_permission_note_delete");
        registry.Register("contacts", "relationship", "create", "lockey_contacts_permission_relationship_create");
        registry.Register("contacts", "relationship", "delete", "lockey_contacts_permission_relationship_delete");
        registry.Register("contacts", "import", "execute", "lockey_contacts_permission_import_execute");
        registry.Register("contacts", "export", "execute", "lockey_contacts_permission_export_execute");
        registry.Register("contacts", "gdpr", "export",          "lockey_contacts_permission_gdpr_export");
        registry.Register("contacts", "gdpr", "delete",          "lockey_contacts_permission_gdpr_delete");
        registry.Register("contacts", "gdpr", "settings_manage", "lockey_contacts_permission_gdpr_settings_manage");
        registry.Register("contacts", "merge", "execute", "lockey_contacts_permission_merge_execute");
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
