using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Documents.Api;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

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
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        // Signature expiry job will be registered when signature CQRS is implemented in Phase 2.
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
