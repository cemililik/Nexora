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
    /// <c>DemoDataSeedingBoundaryTests</c>.
    /// </para>
    /// <para>
    /// The orchestrator's marker table reduces the chance of re-running a
    /// module that has already finished, but the marker write and the module's
    /// own work are NOT atomic across the two DbContexts. A successful module
    /// pass followed by a marker-write crash will re-invoke the module on the
    /// next run, so modules MUST implement their own idempotency
    /// (e.g., upsert on natural keys, no-op on duplicate inserts) — do NOT
    /// rely on "the orchestrator guarantees this only runs once". The marker
    /// is best-effort, the module's own design is the safety net.
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

    /// <summary>
    /// Removes every row this module seeded via <see cref="SeedDemoDataAsync"/>
    /// from the tenant named by <paramref name="context"/>. Called by
    /// <c>IDemoDataCleaner</c> (T-009) when an operator invokes
    /// <c>nexora demo:clean --tenant=&lt;id&gt;</c> or the matching admin UI
    /// button. The default implementation is a no-op so modules without demo
    /// data compile unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modules MUST scope all deletes to the current tenant schema and MUST
    /// NOT delete data from another module's tables — the same boundary
    /// enforced for <see cref="SeedDemoDataAsync"/> by
    /// <c>DemoDataSeedingBoundaryTests</c>.
    /// </para>
    /// <para>
    /// <b>Mixed real + demo data.</b> When a tenant carries both real rows
    /// (produced by day-to-day use) and demo rows (produced by
    /// <see cref="SeedDemoDataAsync"/>), only the demo rows MUST be deleted.
    /// The recommended strategy is to tag demo rows with a stable marker
    /// at seed time (e.g. a <c>Source = "demo"</c> column, a
    /// <c>DemoBatchId</c> FK, or a deterministic ID prefix) and filter on
    /// that marker here. Module-by-module choice is intentional — some
    /// modules may legitimately choose full-scenario wipes when their
    /// entities are strictly demo-exclusive.
    /// </para>
    /// <para>
    /// This method is the counterpart of the CASCADE path taken by
    /// <c>demo:clean --drop-tenant</c> — which bypasses every
    /// <see cref="CleanDemoDataAsync"/> call entirely and drops the whole
    /// tenant schema. Implementations SHOULD remain idempotent so an
    /// operator can run <c>demo:clean</c> repeatedly against the same
    /// tenant without surprising errors; see T-009 acceptance criteria.
    /// </para>
    /// </remarks>
    Task CleanDemoDataAsync(TenantDemoSeedContext context, CancellationToken ct)
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
/// tenant identity plus a non-owned scoped service provider so the module can
/// resolve its own DbContext / repositories without reaching outside its own
/// assembly. <see cref="ScopedServices"/> is a <see cref="INonOwnedServiceProvider"/>
/// — disposing it is a no-op, which is the compile-time enforcement of "the
/// orchestrator owns the scope lifetime; modules MUST NOT dispose it".
/// </summary>
public sealed record TenantDemoSeedContext(
    string TenantId,
    string SchemaName,
    string? OrganizationId,
    INonOwnedServiceProvider ScopedServices,
    string Scenario);

/// <summary>
/// Marker wrapper around <see cref="IServiceProvider"/> that signals "the
/// caller owns disposal, you don't". Implementations are <b>required</b> to be
/// no-op on <see cref="IDisposable.Dispose"/> if they implement it at all —
/// modules that hold a reference must not be able to tear down the
/// orchestrator's scope by mistake.
/// </summary>
/// <remarks>
/// The interface deliberately omits <see cref="IDisposable"/> so a module
/// calling <c>using var sp = context.ScopedServices;</c> fails at compile
/// time. <see cref="GetService"/> and <see cref="GetRequiredService"/> are
/// provided as extension targets so DI usage stays familiar.
/// </remarks>
public interface INonOwnedServiceProvider
{
    /// <summary>Resolve a service or return <c>null</c>.</summary>
    object? GetService(Type serviceType);
}

/// <summary>
/// Helpers so callers can use the standard
/// <c>provider.GetRequiredService&lt;T&gt;()</c> shape against a
/// <see cref="INonOwnedServiceProvider"/>.
/// </summary>
public static class NonOwnedServiceProviderExtensions
{
    /// <summary>Resolve <typeparamref name="T"/> or return <c>default</c>.</summary>
    public static T? GetService<T>(this INonOwnedServiceProvider provider)
        => (T?)provider.GetService(typeof(T));

    /// <summary>Resolve <typeparamref name="T"/> or throw.</summary>
    public static T GetRequiredService<T>(this INonOwnedServiceProvider provider)
    {
        var svc = provider.GetService(typeof(T));
        if (svc is null)
            throw new InvalidOperationException(
                $"No service registered for type '{typeof(T).FullName}'.");
        return (T)svc;
    }
}

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
