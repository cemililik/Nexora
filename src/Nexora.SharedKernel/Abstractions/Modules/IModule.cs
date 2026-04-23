using System.Linq.Expressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Authorization;

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

    /// <summary>
    /// Run on application startup. Each module MUST register every permission it enforces
    /// via <paramref name="registry"/>; unregistered names cannot be enforced
    /// (<c>docs/standards/permissions.md</c> §3, ADR-004).
    /// Modules may also warm caches or perform other idempotent startup work here.
    /// </summary>
    Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct);

    /// <summary>Run when module is installed for a tenant</summary>
    Task OnInstallAsync(TenantInstallContext context, CancellationToken ct);

    /// <summary>Run when module is uninstalled for a tenant</summary>
    Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct);

    /// <summary>
    /// Seeds demo content for the module into the tenant named by
    /// <paramref name="context"/>. Called by <c>IDemoDataSeeder</c> (T-005) after
    /// <see cref="OnInstallAsync"/> has provisioned the module's tables. The
    /// default implementation is a no-op so modules without demo data compile
    /// unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modules MUST scope all writes to the current tenant and MUST NOT reach
    /// into another module's DbContext — that is enforced by
    /// <c>DemoDataSeedingBoundaryTests</c>. The orchestrator guarantees
    /// per-<c>(tenantId, moduleName, scenario)</c> idempotency via the
    /// <c>platform_demo_seed_markers</c> table; modules should treat the call as
    /// "this is the first time demo data is being seeded for this tenant +
    /// scenario" and can skip their own dedup checks.
    /// </para>
    /// <para>
    /// The <c>Scenario</c> is a string identifier chosen by the orchestrator
    /// caller (T-006 CLI / T-008 admin UI). "general" and "ngo" are the initial
    /// scenarios declared by T-007; modules MAY support a subset and return
    /// immediately for unknown values.
    /// </para>
    /// </remarks>
    Task SeedDemoDataAsync(TenantDemoSeedContext context, CancellationToken ct)
        => Task.CompletedTask;
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
/// Context provided to modules during demo-data seeding (T-005). Carries the
/// tenant identity plus a scoped <see cref="IServiceProvider"/> so the module
/// can resolve its own DbContext / repositories without reaching outside its
/// own assembly. The orchestrator owns the scope lifetime; modules MUST NOT
/// dispose <see cref="ScopedServices"/>.
/// </summary>
public sealed record TenantDemoSeedContext(
    string TenantId,
    string SchemaName,
    string? OrganizationId,
    IServiceProvider ScopedServices,
    string Scenario);

/// <summary>
/// Scheduler for recurring/scheduled jobs.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Registers or updates a recurring job with the given cron schedule and explicit method call.</summary>
    void AddOrUpdate<TJob>(
        string jobId,
        string cronExpression,
        Expression<Func<TJob, Task>> methodCall,
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
