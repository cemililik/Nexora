using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Default <see cref="IDemoDataSeeder"/> — walks every registered
/// <see cref="IModule"/> in dependency order, creates a DI scope per module,
/// pushes tenant context onto the ambient accessor, then calls
/// <see cref="IModule.SeedDemoDataAsync"/>. Writes a marker row on success so
/// subsequent runs short-circuit.
/// </summary>
public sealed class DemoDataSeeder(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IModule> modules,
    ILogger<DemoDataSeeder> logger) : IDemoDataSeeder
{
    /// <inheritdoc />
    public async Task<DemoSeedRunResult> SeedAsync(
        string tenantId, string scenario, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException(
                $"TenantId must be a GUID; got '{tenantId}'.", nameof(tenantId));

        var ordered = OrderByDependencies(modules.ToList());
        var outcomes = new List<DemoSeedModuleOutcome>(ordered.Count);

        foreach (var module in ordered)
        {
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await SeedModuleAsync(module, tenantGuid, scenario, ct));
        }

        logger.LogInformation(
            "Demo-seed run for tenant {TenantId} scenario {Scenario} finished: {Seeded} seeded, {Skipped} already-seeded, {Failed} failed.",
            tenantId, scenario,
            outcomes.Count(o => o.Status == DemoSeedStatus.Seeded),
            outcomes.Count(o => o.Status == DemoSeedStatus.AlreadySeeded),
            outcomes.Count(o => o.Status == DemoSeedStatus.Failed));

        return new DemoSeedRunResult(tenantId, scenario, outcomes);
    }

    private async Task<DemoSeedModuleOutcome> SeedModuleAsync(
        IModule module, Guid tenantGuid, string scenario, CancellationToken ct)
    {
        // Each module runs in its own scope so scoped services (DbContext,
        // repositories) are disposed before the next module starts — avoids
        // cross-module DbContext leaks and keeps the tenant context per-module.
        using var scope = scopeFactory.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());

        var tenantCtx = accessor.Current;

        // Idempotency check — uses the tenant-schema marker table so two parallel
        // callers for the same (tenant, module, scenario) converge on a single row.
        var markerDb = scope.ServiceProvider.GetRequiredService<DemoSeedMarkerDbContext>();

        var alreadySeeded = await markerDb.Markers.AsNoTracking().AnyAsync(
            m => m.TenantId == tenantGuid &&
                 m.ModuleName == module.Name &&
                 m.Scenario == scenario,
            ct);
        if (alreadySeeded)
        {
            logger.LogDebug(
                "Demo-seed: module {Module} already seeded for tenant {TenantId} scenario {Scenario}.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.AlreadySeeded);
        }

        var seedContext = new TenantDemoSeedContext(
            TenantId: tenantGuid.ToString(),
            SchemaName: tenantCtx.SchemaName,
            OrganizationId: tenantCtx.OrganizationId,
            ScopedServices: scope.ServiceProvider,
            Scenario: scenario);

        try
        {
            await module.SeedDemoDataAsync(seedContext, ct);

            markerDb.Markers.Add(new DemoSeedMarker
            {
                TenantId = tenantGuid,
                ModuleName = module.Name,
                Scenario = scenario,
                SeededAt = DateTimeOffset.UtcNow
            });
            await markerDb.SaveChangesAsync(ct);

            logger.LogInformation(
                "Demo-seed: module {Module} seeded for tenant {TenantId} scenario {Scenario}.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Seeded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // We intentionally DO NOT rethrow — one failing module must not block
            // subsequent ones; the caller gets a per-module failure in the result.
            // Errors are surfaced as lockey where available; raw message is logged
            // at Error for operator diagnosis.
            logger.LogError(ex,
                "Demo-seed: module {Module} failed for tenant {TenantId} scenario {Scenario}.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Topological sort over <see cref="IModule.Dependencies"/>. Modules with no
    /// dependency on each other are emitted in declaration order so results are
    /// deterministic. Throws when a cycle is detected — this is a production-time
    /// misconfiguration, not a runtime condition.
    /// </summary>
    public static List<IModule> OrderByDependencies(IReadOnlyList<IModule> input)
    {
        var byName = input.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var result = new List<IModule>();

        void Visit(IModule module, Stack<string> path)
        {
            if (visited.TryGetValue(module.Name, out var done))
            {
                if (done) return;
                throw new InvalidOperationException(
                    $"Cycle detected in IModule dependency graph: {string.Join(" -> ", path.Reverse())} -> {module.Name}");
            }
            visited[module.Name] = false;
            path.Push(module.Name);

            foreach (var depName in module.Dependencies)
            {
                if (!byName.TryGetValue(depName, out var dep))
                {
                    // Missing dependency is a module-registration bug — fail loudly.
                    throw new InvalidOperationException(
                        $"Module '{module.Name}' depends on '{depName}' but no such module is registered.");
                }
                Visit(dep, path);
            }

            path.Pop();
            visited[module.Name] = true;
            result.Add(module);
        }

        foreach (var module in input)
        {
            if (!visited.ContainsKey(module.Name))
                Visit(module, new Stack<string>());
        }
        return result;
    }
}
