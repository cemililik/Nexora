using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Documents.Api;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Jobs;
using Nexora.Modules.Documents.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
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
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Domain event handlers are auto-registered via MediatR assembly scanning.
        // Cross-module integration event handlers (education, donations, hr) will be added
        // when those modules are implemented.
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
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(CancellationToken ct)
    {
        // Documents module permissions are seeded centrally in IdentityModuleMigration.SeedAsync()
        // (8 permissions: documents.document.read, documents.document.upload, documents.document.update,
        //  documents.document.delete, documents.folder.manage, documents.template.manage,
        //  documents.signature.read, documents.signature.create).
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
