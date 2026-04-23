using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Documents.Api;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.IntegrationEvents;
using Nexora.Modules.Documents.Infrastructure.Jobs;
using Nexora.Modules.Documents.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Domain.Events;
using DocumentService = Nexora.Modules.Documents.Infrastructure.Services.DocumentService;

namespace Nexora.Modules.Documents;

/// <summary>Documents module providing folder management, versioned file storage, access control, templates, and digital signatures.</summary>
public sealed class DocumentsModule : IModule
{
    /// <inheritdoc />
    public string Name => "documents";
    /// <inheritdoc />
    public string DisplayName => "lockey_documents_module_display_name";
    /// <inheritdoc />
    public string Version => "1.0.0";
    /// <inheritdoc />
    public IReadOnlyList<string> Dependencies => ["identity"];

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DocumentsDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "documents");
            });
            options.AddNexoraAuditInterceptor(sp);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DocumentsModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, DocumentsModuleMigration>();

        // Access control
        services.AddScoped<IDocumentAccessChecker, DocumentAccessChecker>();

        // Archival service
        services.AddScoped<IDocumentArchivalService, DocumentArchivalService>();

        // Cross-module document service
        services.AddScoped<IDocumentService, DocumentService>();

        // Outbox for transactional event publishing
        services.AddScoped<IOutbox, OutboxService<DocumentsDbContext>>();

        // Inbox guard for idempotent integration event consumption (ADR-0011)
        services.AddScoped<IInboxGuard, InboxGuard<DocumentsDbContext>>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Domain event handlers are auto-registered via MediatR assembly scanning.
        // Integration event handlers for cross-module events:
        services.AddScoped<IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>,
            ContactGdprDeletedIntegrationEventHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFolderEndpoints();
        endpoints.MapDocumentEndpoints();
        endpoints.MapDocumentVersionEndpoints();
        endpoints.MapDocumentAccessEndpoints();
        endpoints.MapSignatureEndpoints();
        endpoints.MapTemplateEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<SignatureExpiryJob>(
            "documents:signature-expiry",
            "0 1 * * *", // Every day at 01:00 UTC
            job => job.RunAsync(new SignatureExpiryJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Default);

        scheduler.AddOrUpdate<SignatureReminderJob>(
            "documents:signature-reminder",
            "0 8 * * *", // Every day at 08:00 UTC
            job => job.RunAsync(new SignatureReminderJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Default);

        scheduler.AddOrUpdate<FolderAccessExpiryJob>(
            "documents:folder-access-expiry",
            "0 2 * * *", // Every day at 02:00 UTC
            job => job.RunAsync(new FolderAccessExpiryJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Default);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct)
    {
        registry.Register("documents", "document", "read",   "lockey_documents_permission_document_read");
        registry.Register("documents", "document", "upload", "lockey_documents_permission_document_upload");
        registry.Register("documents", "document", "update", "lockey_documents_permission_document_update");
        registry.Register("documents", "document", "delete", "lockey_documents_permission_document_delete");
        registry.Register("documents", "folder", "read",   "lockey_documents_permission_folder_read");
        registry.Register("documents", "folder", "manage", "lockey_documents_permission_folder_manage");
        registry.Register("documents", "signature", "read",   "lockey_documents_permission_signature_read");
        registry.Register("documents", "signature", "create", "lockey_documents_permission_signature_create");
        registry.Register("documents", "signature", "manage", "lockey_documents_permission_signature_manage");
        registry.Register("documents", "template", "read",   "lockey_documents_permission_template_read");
        registry.Register("documents", "template", "manage", "lockey_documents_permission_template_manage");
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
