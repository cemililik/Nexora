using System.Data.Common;
using System.Reflection;
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
/// <see cref="IModule.SeedDemoDataAsync"/>. Marker rows in
/// <c>demo_seed_markers</c> short-circuit subsequent runs.
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

        // Contract-violation indicator (caller bug): CLI + HTTP callers validate
        // the GUID shape before this point. Message is a lockey key so the
        // operator-facing surface (CLI --verbose, structured logs) carries a
        // translatable token, not a hardcoded English string.
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException(
                "lockey_identity_demo_data_tenant_id_must_be_guid", nameof(tenantId));

        // Filter to modules that are actually installed for the tenant when
        // IModuleAvailability is wired up (planned with the cascade-aware
        // uninstall pipeline, ADR-0028 / T-026). Falls back to the full DI
        // registration when no implementation is registered yet — matches
        // current Phase 1.5 behaviour without forcing a chicken-and-egg.
        var allModules = modules.ToList();
        var ordered = OrderByDependencies(allModules);
        var filtered = await FilterInstalledAsync(ordered, tenantGuid, ct);

        // Bulk-fetch every existing marker for this (tenant, scenario) in one
        // round-trip — replaces the previous per-module AnyAsync that issued
        // N queries.
        var existingMarkers = await LoadExistingMarkersAsync(tenantGuid, scenario, ct);
        var outcomes = new List<DemoSeedModuleOutcome>(filtered.Count);

        foreach (var module in filtered)
        {
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await SeedModuleAsync(
                module, tenantGuid, scenario, existingMarkers, ct));
        }

        logger.LogInformation(
            "Demo-seed run for tenant {TenantId} scenario {Scenario} finished: " +
            "{Seeded} seeded, {AlreadySeeded} already-seeded, {NoOp} no-op, {Failed} failed.",
            tenantId, scenario,
            outcomes.Count(o => o.Status == DemoSeedStatus.Seeded),
            outcomes.Count(o => o.Status == DemoSeedStatus.AlreadySeeded),
            outcomes.Count(o => o.Status == DemoSeedStatus.NoOp),
            outcomes.Count(o => o.Status == DemoSeedStatus.Failed));

        return new DemoSeedRunResult(tenantId, scenario, outcomes);
    }

    private async Task<DemoSeedModuleOutcome> SeedModuleAsync(
        IModule module, Guid tenantGuid, string scenario,
        IDictionary<string, DemoSeedMarker> existingMarkers, CancellationToken ct)
    {
        // Each module runs in its own scope so scoped services (DbContext,
        // repositories) are disposed before the next module starts — avoids
        // cross-module DbContext leaks and keeps the tenant context per-module.
        // Async disposal honours scoped DbContext's DisposeAsync (open Npgsql
        // connections, change-tracker buffers).
        await using var scope = scopeFactory.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());

        var tenantCtx = accessor.Current;
        var markerDb = scope.ServiceProvider.GetRequiredService<DemoSeedMarkerDbContext>();

        // NoOp check runs BEFORE the marker lookup: a module that ships no
        // demo content never needs an idempotency marker (re-invoking a no-op
        // is cheaper than a marker read), and reporting NoOp unconditionally
        // gives the operator accurate "this module ships no demo content"
        // feedback regardless of any stale InProgress marker left over from
        // a prior crash or a migration from the previous non-no-op impl.
        if (IsDefaultNoOp(module))
        {
            logger.LogDebug(
                "Demo-seed: module {Module} ships no demo content (default IModule impl).",
                module.Name);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.NoOp);
        }

        // Idempotency check from the bulk-fetched dictionary, not a DB hit.
        if (existingMarkers.TryGetValue(module.Name, out var existing))
        {
            if (existing.Status == DemoSeedMarkerStatus.Seeded)
            {
                logger.LogDebug(
                    "Demo-seed: module {Module} already seeded for tenant {TenantId} scenario {Scenario}.",
                    module.Name, tenantGuid, scenario);
                return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.AlreadySeeded);
            }
            // InProgress means a prior run crashed mid-seed. We retry — modules
            // MUST implement their own idempotency (the docs on
            // IModule.SeedDemoDataAsync warn about this). Mark the existing row
            // back to InProgress (no-op if already) and re-attempt.
            logger.LogWarning(
                "Demo-seed: module {Module} marker found in InProgress state — retrying. Modules must remain idempotent.",
                module.Name);
        }

        // Two-phase write: InProgress before the module runs, Seeded after.
        // Atomicity is best-effort across the module-side work and the marker
        // write — modules MUST be idempotent regardless. The marker just
        // narrows the "rerun a module that already finished" window.
        //
        // The `existing` reference was loaded with AsNoTracking() in the
        // bulk-fetch path, so we DO NOT call Update(existing) (that attaches
        // a detached entity and EF marks every column modified — full-column
        // UPDATE). Instead, re-query inside this scope for a tracked entity
        // and mutate that, OR for the new-marker path, Add a tracked entity.
        DemoSeedMarker marker;
        if (existing is not null)
        {
            // Re-query as a tracked entity. Use FirstOrDefaultAsync (NOT
            // FirstAsync) because the row CAN have been deleted between the
            // bulk-fetch and this point — operator dropped the marker, a
            // parallel cleanup ran, the schema was reset, etc. When that
            // happens, fall through to the Add path so the seed completes
            // instead of throwing InvalidOperationException at the operator.
            var tracked = await markerDb.Markers.FirstOrDefaultAsync(
                m => m.TenantId == tenantGuid &&
                     m.ModuleName == module.Name &&
                     m.Scenario == scenario,
                ct);
            if (tracked is not null)
            {
                marker = tracked;
                marker.Status = DemoSeedMarkerStatus.InProgress;
                marker.StartedAt = DateTimeOffset.UtcNow;
                marker.CompletedAt = null;
            }
            else
            {
                marker = DemoSeedMarker.CreateInProgress(tenantGuid, module.Name, scenario);
                markerDb.Markers.Add(marker);
            }
        }
        else
        {
            marker = DemoSeedMarker.CreateInProgress(tenantGuid, module.Name, scenario);
            markerDb.Markers.Add(marker);
        }

        try
        {
            await markerDb.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // PostgreSQL SQLSTATE 23505 (unique_violation) on the marker insert
            // means a concurrent demo:load invocation won the race to claim this
            // (tenant, module, scenario) tuple. Treat as a friendly operator
            // signal — we surface a dedicated log line so the distinction is
            // obvious in the summary and don't mistake it for a generic insert
            // failure.
            logger.LogWarning(ex,
                "Demo-seed: concurrent demo:load race for module {Module} tenant {TenantId} scenario {Scenario} — marker already present; this runner backs off.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex,
                "Demo-seed: failed to write InProgress marker for module {Module} tenant {TenantId}.",
                module.Name, tenantGuid);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }

        var seedContext = new TenantDemoSeedContext(
            TenantId: tenantGuid.ToString(),
            SchemaName: tenantCtx.SchemaName,
            OrganizationId: tenantCtx.OrganizationId,
            ScopedServices: new NonOwnedServiceProvider(scope.ServiceProvider),
            Scenario: scenario);

        try
        {
            await module.SeedDemoDataAsync(seedContext, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled — propagate. The InProgress marker stays so a
            // resumed run knows it crashed mid-seed.
            throw;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex,
                "Demo-seed: module {Module} failed (DbUpdate) for tenant {TenantId} scenario {Scenario}.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }
        catch (DbException ex)
        {
            logger.LogError(ex,
                "Demo-seed: module {Module} failed (DbException) for tenant {TenantId} scenario {Scenario}.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Common surface for missing tables / mis-mapped DbContexts during
            // demo-seed — surface clearly without a stack trace.
            logger.LogError(ex,
                "Demo-seed: module {Module} failed (InvalidOperation) for tenant {TenantId} scenario {Scenario}.",
                module.Name, tenantGuid, scenario);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }
        // Note: we deliberately do NOT catch Exception — fatal CLR conditions
        // (StackOverflowException, OutOfMemoryException, ThreadAbortException)
        // and unexpected module bugs propagate so the run terminates loudly
        // instead of silently logging "module failed: X".

        // Module finished cleanly — promote marker to Seeded.
        marker.Status = DemoSeedMarkerStatus.Seeded;
        marker.CompletedAt = DateTimeOffset.UtcNow;
        try
        {
            await markerDb.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Module work succeeded but the marker write failed — mark Failed
            // so the operator sees the discrepancy. Next run will retry the
            // module; idempotency in the module is the safety net.
            logger.LogError(ex,
                "Demo-seed: module {Module} succeeded but marker write failed for tenant {TenantId}.",
                module.Name, tenantGuid);
            return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Failed, ex.Message);
        }

        logger.LogInformation(
            "Demo-seed: module {Module} seeded for tenant {TenantId} scenario {Scenario}.",
            module.Name, tenantGuid, scenario);
        return new DemoSeedModuleOutcome(module.Name, DemoSeedStatus.Seeded);
    }

    /// <summary>
    /// Returns the subset of <paramref name="ordered"/> that is installed for
    /// the tenant. Uses <see cref="IModuleAvailability"/> if registered;
    /// otherwise treats every DI-registered module as installed (Phase 1.5
    /// behaviour — no per-tenant install/uninstall pipeline yet).
    /// </summary>
    private async Task<List<IModule>> FilterInstalledAsync(
        IList<IModule> ordered, Guid tenantGuid, CancellationToken ct)
    {
        // Probe for IModuleAvailability WITHOUT opening a scope first — the
        // common Phase 1.5 path is "no implementation registered" and there
        // is no point materialising an async scope + tenant context just to
        // discover that. If the service is missing, return the full ordered
        // list directly.
        using (var probeScope = scopeFactory.CreateScope())
        {
            if (probeScope.ServiceProvider.GetService<IModuleAvailability>() is null)
            {
                return ordered.ToList();
            }
        }

        // Implementation IS registered — now we need a tenant-context-bound
        // async scope to call its API.
        await using var scope = scopeFactory.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());
        var availability = scope.ServiceProvider.GetRequiredService<IModuleAvailability>();

        var installed = (await availability.GetInstalledModulesAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var skipped = ordered.Where(m => !installed.Contains(m.Name)).ToList();
        if (skipped.Count > 0)
        {
            logger.LogDebug(
                "Demo-seed: skipping {Count} not-installed modules for tenant {TenantId}: {Modules}",
                skipped.Count, tenantGuid, string.Join(", ", skipped.Select(m => m.Name)));
        }
        return ordered.Where(m => installed.Contains(m.Name)).ToList();
    }

    private async Task<IDictionary<string, DemoSeedMarker>> LoadExistingMarkersAsync(
        Guid tenantGuid, string scenario, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());

        var markerDb = scope.ServiceProvider.GetRequiredService<DemoSeedMarkerDbContext>();
        var rows = await markerDb.Markers.AsNoTracking()
            .Where(m => m.TenantId == tenantGuid && m.Scenario == scenario)
            .ToListAsync(ct);
        return rows.ToDictionary(m => m.ModuleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects PostgreSQL unique-violation (<c>SQLSTATE 23505</c>) wrapped in
    /// an EF Core <see cref="DbUpdateException"/>. Used to differentiate a
    /// concurrent demo:load race (another runner won the insert) from a
    /// generic marker-write failure.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Reflect via the full type name so the Infrastructure project need not
        // take a hard reference on Npgsql at compile time — the provider is
        // transitively referenced through EF Core, but this method must still
        // compile when Npgsql is swapped out (e.g. InMemory provider in tests).
        for (var current = ex.InnerException; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName != "Npgsql.PostgresException") continue;
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (sqlState == "23505") return true;
        }
        return false;
    }

    /// <summary>
    /// Detects whether <paramref name="module"/> uses the C# default interface
    /// implementation of <see cref="IModule.SeedDemoDataAsync"/> — i.e. ships
    /// no demo content. The default impl declares <c>DeclaringType ==
    /// typeof(IModule)</c>; an overriding implementation declares it as the
    /// concrete module type.
    /// </summary>
    public static bool IsDefaultNoOp(IModule module)
    {
        var concrete = module.GetType();
        // Find the interface map for IModule on the concrete type.
        // Use the (name, flags, binder, types, modifiers) overload so the
        // signature is matched exactly — guards against future overloads
        // making the parameterless GetMethod throw AmbiguousMatchException.
        var ifaceMethod = typeof(IModule).GetMethod(
            name: nameof(IModule.SeedDemoDataAsync),
            bindingAttr: BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(TenantDemoSeedContext), typeof(CancellationToken) },
            modifiers: null)
            ?? throw new InvalidOperationException(
                $"Cannot resolve {nameof(IModule)}.{nameof(IModule.SeedDemoDataAsync)}(TenantDemoSeedContext, CancellationToken) — " +
                "the interface signature changed without updating DemoDataSeeder.IsDefaultNoOp.");

        var map = concrete.GetInterfaceMap(typeof(IModule));
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (map.InterfaceMethods[i] != ifaceMethod) continue;

            // If the target method's declaring type is IModule itself, the
            // module did not provide its own implementation — it's the default
            // interface method (C# 8+ DIM).
            return map.TargetMethods[i].DeclaringType == typeof(IModule);
        }
        // Defensive: if we can't find a mapping, treat as not-default so we
        // err on the side of running the seeder.
        return false;
    }

    /// <summary>
    /// Topological sort over <see cref="IModule.Dependencies"/>. Delegates to
    /// <see cref="ModuleDependencyGraph.OrderByDependencies"/> — kept here as
    /// a thin wrapper so existing call sites (MigrationRunner, DemoDataCleaner,
    /// internal seeder loop) don't churn.
    /// </summary>
    public static List<IModule> OrderByDependencies(IReadOnlyList<IModule> input)
        => ModuleDependencyGraph.OrderByDependencies(input);
}
