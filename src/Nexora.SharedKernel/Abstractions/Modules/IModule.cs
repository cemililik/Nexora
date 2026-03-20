using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Contract for a Nexora module. Every module must implement this interface.
/// </summary>
public interface IModule
{
    /// <summary>Unique module identifier (e.g., "identity", "crm", "donations")</summary>
    string Name { get; }

    /// <summary>Human-readable display name</summary>
    string DisplayName { get; }

    /// <summary>Module version (SemVer)</summary>
    string Version { get; }

    /// <summary>Required module dependencies</summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>Register services into DI container</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Register API endpoints</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>Register integration event handlers for cross-module communication</summary>
    void ConfigureEventHandlers(IServiceCollection services);

    /// <summary>Register recurring/background jobs</summary>
    void ConfigureJobs(IJobScheduler scheduler);

    /// <summary>Check module health (database connectivity, external services, etc.)</summary>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);

    /// <summary>Run on application startup (register permissions, caches)</summary>
    Task OnStartupAsync(CancellationToken ct);

    /// <summary>Run when module is installed for a tenant</summary>
    Task OnInstallAsync(TenantInstallContext context, CancellationToken ct);

    /// <summary>Run when module is uninstalled for a tenant</summary>
    Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct);
}

/// <summary>
/// Result of a module health check.
/// </summary>
public sealed record HealthCheckResult(bool IsHealthy, string? Message = null)
{
    /// <summary>Creates a healthy result.</summary>
    public static HealthCheckResult Healthy() => new(true);

    /// <summary>Creates an unhealthy result with an error message.</summary>
    public static HealthCheckResult Unhealthy(string message) => new(false, message);
}

/// <summary>
/// Context provided to modules during tenant install/uninstall operations.
/// </summary>
public sealed record TenantInstallContext(
    string TenantId,
    string SchemaName,
    string? OrganizationId);

/// <summary>
/// Scheduler for recurring/scheduled jobs.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Registers or updates a recurring job with the given cron schedule.</summary>
    void AddOrUpdate<TJob>(
        string jobId,
        string cronExpression,
        string queue = "default") where TJob : class;
}

/// <summary>
/// Checks if a module is installed for the current tenant.
/// </summary>
public interface IModuleAvailability
{
    /// <summary>Checks whether a module is installed for the current tenant.</summary>
    Task<bool> IsInstalledAsync(string moduleName, CancellationToken ct = default);

    /// <summary>Gets the list of installed module names for the current tenant.</summary>
    Task<IReadOnlyList<string>> GetInstalledModulesAsync(CancellationToken ct = default);
}
