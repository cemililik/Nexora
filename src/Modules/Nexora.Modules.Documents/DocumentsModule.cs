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

public sealed class DocumentsModule : IModule
{
    public string Name => "documents";
    public string DisplayName => "lockey_documents_module_display_name";
    public string Version => "1.0.0";
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
        var module = endpoints.MapGroup("/api/v1/documents");

        module.MapFolderEndpoints();
        module.MapDocumentEndpoints();
        module.MapDocumentVersionEndpoints();
        module.MapDocumentAccessEndpoints();
        module.MapSignatureEndpoints();
        module.MapTemplateEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<SignatureExpiryJob>(
            "documents:signature-expiry",
            "0 1 * * *", // Every day at 01:00 UTC
            JobQueues.Default);

        scheduler.AddOrUpdate<SignatureReminderJob>(
            "documents:signature-reminder",
            "0 8 * * *", // Every day at 08:00 UTC
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
        // TODO: Register Documents module permissions (e.g., documents.documents.upload,
        // documents.documents.read, documents.documents.delete, documents.folders.manage,
        // documents.signatures.create, documents.templates.manage)
        // once IPermissionRegistrar is available in SharedKernel.
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
