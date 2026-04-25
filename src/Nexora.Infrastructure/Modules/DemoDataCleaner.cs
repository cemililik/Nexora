using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Default <see cref="IDemoDataCleaner"/> (T-009) — the reverse of
/// <see cref="DemoDataSeeder"/>. Walks modules in <b>reverse</b> dependency
/// order so a module is cleaned only after every module that depends on it
/// has been cleaned first; opens a scope per module; calls
/// <see cref="IModule.CleanDemoDataAsync"/>; then removes the matching
/// <c>demo_seed_markers</c> row so a future
/// <see cref="IDemoDataSeeder.SeedAsync"/> run re-seeds fresh content instead
/// of short-circuiting on a stale "Seeded" marker.
///
/// <para>
/// <see cref="DropTenantAsync"/> is the other entry point: it drops the whole
/// tenant schema via <c>DROP SCHEMA ... CASCADE</c> and emits
/// <see cref="TenantDeprovisionedIntegrationEvent"/> so downstream consumers
/// (MinIO buckets, Keycloak realm, cache layers) can teardown tenant-scoped
/// external state. Module-level <see cref="IModule.CleanDemoDataAsync"/> is
/// intentionally NOT called — the CASCADE drop is atomic and strictly cheaper.
/// </para>
/// </summary>
public sealed class DemoDataCleaner(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IModule> modules,
    IEventBus eventBus,
    ILogger<DemoDataCleaner> logger) : IDemoDataCleaner
{
    /// <inheritdoc />
    public async Task<DemoCleanRunResult> CleanAsync(
        string tenantId, string scenario, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        // Contract-violation indicator (caller bug): CLI + HTTP callers validate
        // the GUID shape before this point. The message is a lockey key so the
        // CLI's --verbose path surfaces a translatable string instead of a
        // hardcoded English sentence.
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException(
                "lockey_identity_demo_data_tenant_id_must_be_guid", nameof(tenantId));

        var allModules = modules.ToList();
        // Reverse dependency order: a module can only be cleaned after every
        // module that depends on it is cleaned. Reusing the seeder's
        // topological sort keeps the ordering consistent between seed and
        // clean passes.
        var ordered = DemoDataSeeder.OrderByDependencies(allModules);
        ordered.Reverse();
        // Filter to modules actually installed for the tenant — same
        // semantics as the seeder's FilterInstalledAsync. Without this,
        // Cleaning would invoke CleanDemoDataAsync on every DI-registered
        // module regardless of per-tenant install state, producing
        // spurious Failed outcomes for modules whose tables were never
        // provisioned for this tenant.
        var filtered = await FilterInstalledAsync(ordered, tenantGuid, ct);

        var existingMarkers = await LoadExistingMarkersAsync(tenantGuid, scenario, ct);
        var outcomes = new List<DemoCleanModuleOutcome>(filtered.Count);

        foreach (var module in filtered)
        {
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await CleanModuleAsync(module, tenantGuid, scenario, existingMarkers, ct));
        }

        logger.LogInformation(
            "Demo-clean run for tenant {TenantId} scenario {Scenario} finished: " +
            "{Cleaned} cleaned, {NothingToClean} nothing-to-clean, {NoOp} no-op, {Failed} failed.",
            tenantId, scenario,
            outcomes.Count(o => o.Status == DemoCleanStatus.Cleaned),
            outcomes.Count(o => o.Status == DemoCleanStatus.NothingToClean),
            outcomes.Count(o => o.Status == DemoCleanStatus.NoOp),
            outcomes.Count(o => o.Status == DemoCleanStatus.Failed));

        return new DemoCleanRunResult(tenantId, scenario, outcomes);
    }

    private async Task<DemoCleanModuleOutcome> CleanModuleAsync(
        IModule module, Guid tenantGuid, string scenario,
        IDictionary<string, DemoSeedMarker> existingMarkers, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());

        var tenantCtx = accessor.Current;
        var markerDb = scope.ServiceProvider.GetRequiredService<DemoSeedMarkerDbContext>();

        var hasMarker = existingMarkers.ContainsKey(module.Name);
        // Whether this module ships the default no-op CleanDemoDataAsync — the
        // only thing that matters for the clean path. Seed-side "default no-op"
        // (the seeder's IsDefaultNoOp) is a separate concern and doesn't affect
        // how we should clean.
        var isDefaultNoOp = IsCleanDefaultNoOp(module);

        // No marker and a module that overrode CleanDemoDataAsync: still invoke
        // cleanup. A real-world case is a manually-re-seeded module where the
        // marker row was lost; the module's own idempotent cleanup is the
        // correct recovery path. The marker row (if present) is removed below.
        //
        // No marker AND default no-op: nothing to do at all.
        if (!hasMarker && isDefaultNoOp)
        {
            return new DemoCleanModuleOutcome(module.Name, DemoCleanStatus.NothingToClean);
        }

        if (!isDefaultNoOp)
        {
            var seedContext = new TenantDemoSeedContext(
                TenantId: tenantGuid.ToString(),
                SchemaName: tenantCtx.SchemaName,
                OrganizationId: tenantCtx.OrganizationId,
                ScopedServices: new NonOwnedServiceProvider(scope.ServiceProvider),
                Scenario: scenario);

            try
            {
                await module.CleanDemoDataAsync(seedContext, ct);
            }
            catch (OperationCanceledException ex)
            {
                // Distinguish the caller's cancellation from a module-internal
                // cancellation (e.g. the module's own timeout token firing).
                // Only the CALLER cancellation should abort the whole CleanAsync
                // loop — a module that cancels itself is reported as a Failed
                // outcome so siblings still run, matching the behaviour of any
                // other module-internal exception below.
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                logger.LogError(ex,
                    "Demo-clean: module {Module} aborted via internal cancellation for tenant {TenantId} scenario {Scenario}. Siblings continue.",
                    module.Name, tenantGuid, scenario);
                return new DemoCleanModuleOutcome(module.Name, DemoCleanStatus.Failed, ex.Message);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex,
                    "Demo-clean: module {Module} failed (DbUpdate) for tenant {TenantId} scenario {Scenario}.",
                    module.Name, tenantGuid, scenario);
                return new DemoCleanModuleOutcome(module.Name, DemoCleanStatus.Failed, ex.Message);
            }
            catch (DbException ex)
            {
                logger.LogError(ex,
                    "Demo-clean: module {Module} failed (DbException) for tenant {TenantId} scenario {Scenario}.",
                    module.Name, tenantGuid, scenario);
                return new DemoCleanModuleOutcome(module.Name, DemoCleanStatus.Failed, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex,
                    "Demo-clean: module {Module} failed (InvalidOperation) for tenant {TenantId} scenario {Scenario}.",
                    module.Name, tenantGuid, scenario);
                return new DemoCleanModuleOutcome(module.Name, DemoCleanStatus.Failed, ex.Message);
            }
        }

        // Remove the marker row (if any) so a subsequent demo:load re-seeds
        // cleanly. Prefer ExecuteDeleteAsync on relational providers — single
        // DELETE round-trip, composite PK (TenantId, ModuleName, Scenario)
        // is fully indexed, count is 0-or-1 so no "affected rows > 1"
        // surprise. Falls back to load + Remove + SaveChanges on the
        // InMemory provider used in unit tests (InMemory does not support
        // ExecuteDeleteAsync — same pattern as
        // ContactGdprDeletedIntegrationEventHandler's relational check).
        try
        {
            if (markerDb.Database.IsRelational())
            {
                await markerDb.Markers
                    .Where(m => m.TenantId == tenantGuid &&
                                m.ModuleName == module.Name &&
                                m.Scenario == scenario)
                    .ExecuteDeleteAsync(ct);
            }
            else
            {
                var tracked = await markerDb.Markers.FirstOrDefaultAsync(
                    m => m.TenantId == tenantGuid &&
                         m.ModuleName == module.Name &&
                         m.Scenario == scenario,
                    ct);
                if (tracked is not null)
                {
                    markerDb.Markers.Remove(tracked);
                    await markerDb.SaveChangesAsync(ct);
                }
            }
        }
        catch (DbUpdateException ex)
        {
            // CleanDemoDataAsync succeeded but the marker row delete failed —
            // surface the distinction in the error message so operators can
            // see "data cleaned but marker row delete failed" instead of
            // thinking the whole module failed. Status stays Failed because
            // the marker row will short-circuit a future demo:load as
            // already-seeded against data that's already gone (re-seeding
            // would write the same content back, which is itself OK but
            // wastes effort and could surface as "nothing to seed — marker
            // present").
            logger.LogError(ex,
                "Demo-clean: module {Module} data cleaned but marker row delete failed for tenant {TenantId}: {Error}",
                module.Name, tenantGuid, ex.Message);
            return new DemoCleanModuleOutcome(
                module.Name,
                DemoCleanStatus.Failed,
                $"Data cleaned but marker row delete failed: {ex.Message}");
        }

        var status = isDefaultNoOp
            ? DemoCleanStatus.NoOp
            : DemoCleanStatus.Cleaned;
        logger.LogInformation(
            "Demo-clean: module {Module} {Status} for tenant {TenantId} scenario {Scenario}.",
            module.Name, status, tenantGuid, scenario);
        return new DemoCleanModuleOutcome(module.Name, status);
    }

    /// <inheritdoc />
    public async Task<DemoDropTenantResult> DropTenantAsync(
        string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        // Contract-violation indicator (caller bug): CLI + HTTP callers validate
        // the GUID shape before this point. The message is a lockey key so the
        // CLI's --verbose path surfaces a translatable string instead of a
        // hardcoded English sentence.
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException(
                "lockey_identity_demo_data_tenant_id_must_be_guid", nameof(tenantId));

        await using var scope = scopeFactory.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());

        // Borrow the marker DbContext's connection — same rationale as the
        // demo:load tenant-schema probe: reuse the host's connection-string
        // resolution and pooling instead of new-ing a raw NpgsqlConnection.
        var dbContext = scope.ServiceProvider.GetRequiredService<DemoSeedMarkerDbContext>();
        // Use the canonical SchemaName from the tenant context, not a locally
        // reconstructed string. TenantContext + BaseDbContext.HasDefaultSchema
        // are the single source of truth; recomputing `tenant_{guid}` here
        // would fork the format from that source if it ever changes.
        var schemaName = accessor.Current.SchemaName;

        try
        {
            // Identifier is internally generated above (`$"tenant_{tenantGuid}"`
            // with tenantGuid from Guid.TryParse), never operator-supplied, so
            // quoted interpolation is safe. PostgreSQL does not parameterize
            // identifiers, so ExecuteSqlRawAsync is the only option for
            // dynamic DDL. Analyzer suppressed because the value is validated
            // at method entry.
#pragma warning disable EF1002
            await dbContext.Database.ExecuteSqlRawAsync(
                $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE", ct);
#pragma warning restore EF1002
        }
        catch (DbException ex)
        {
            logger.LogError(ex,
                "Demo-clean: failed to drop tenant schema {Schema}.", schemaName);
            return new DemoDropTenantResult(tenantId, SchemaDropped: false, ex.Message);
        }

        // Publish via IEventBus directly — the transactional outbox is NOT an
        // option here because the outbox table per the current design lives
        // IN the tenant schema we just dropped. Direct publish is at-most-once
        // (a Dapr/Kafka failure loses the signal), which is an acceptable
        // trade-off: the operator invoked a deliberate "drop this tenant"
        // action, and downstream external cleanup (MinIO, Keycloak) can also
        // be driven by periodic reconciliation sweeps against the platform
        // tenant registry. Filed as a Phase-2 follow-up: a platform-level
        // outbox (tenant-independent) would close the gap and allow
        // transactional semantics here.
        try
        {
            await eventBus.PublishAsync(new TenantDeprovisionedIntegrationEvent
            {
                TenantId = tenantGuid.ToString(),
                SchemaName = schemaName,
                DeprovisionedAtUtc = DateTime.UtcNow
            }, ct);
            logger.LogWarning(
                "Demo-clean: dropped tenant schema {Schema} for tenant {TenantId}; TenantDeprovisionedIntegrationEvent published.",
                schemaName, tenantGuid);
        }
        catch (InvalidOperationException ex)
        {
            return FailPublishAfterDrop(tenantId, schemaName, ex);
        }
        catch (Dapr.DaprException ex)
        {
            return FailPublishAfterDrop(tenantId, schemaName, ex);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return FailPublishAfterDrop(tenantId, schemaName, ex);
        }

        return new DemoDropTenantResult(tenantId, SchemaDropped: true);
    }

    private DemoDropTenantResult FailPublishAfterDrop(
        string tenantId, string schemaName, Exception ex)
    {
        logger.LogError(ex,
            "Demo-clean: tenant schema {Schema} dropped but TenantDeprovisionedIntegrationEvent publish failed; external systems (MinIO / Keycloak) need manual cleanup for tenant {TenantId}.",
            schemaName, tenantId);
        return new DemoDropTenantResult(
            tenantId,
            SchemaDropped: true,
            ErrorMessage: $"Schema dropped; event publish failed: {ex.Message}");
    }

    /// <summary>
    /// Mirror of <see cref="DemoDataSeeder.FilterInstalledAsync"/> — returns
    /// the subset of <paramref name="ordered"/> that is installed for the
    /// tenant via <see cref="IModuleAvailability"/>. Falls back to the full
    /// list when no implementation is registered (Phase 1.5 default), so
    /// the cleaner has the same semantics as the seeder when there is no
    /// per-tenant install pipeline.
    /// </summary>
    private async Task<List<IModule>> FilterInstalledAsync(
        IList<IModule> ordered, Guid tenantGuid, CancellationToken ct)
    {
        // Probe for IModuleAvailability without opening an async scope
        // first — common Phase 1.5 path is "no implementation registered"
        // and there is no point materialising tenant context just to
        // discover that.
        using (var probeScope = scopeFactory.CreateScope())
        {
            if (probeScope.ServiceProvider.GetService<IModuleAvailability>() is null)
            {
                return ordered.ToList();
            }
        }

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
                "Demo-clean: skipping {Count} not-installed modules for tenant {TenantId}: {Modules}",
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
    /// Detects whether <paramref name="module"/> uses the default no-op
    /// <see cref="IModule.CleanDemoDataAsync"/>. Shape mirrors
    /// <see cref="DemoDataSeeder.IsDefaultNoOp"/> but resolves the clean
    /// method rather than the seed method.
    /// </summary>
    internal static bool IsCleanDefaultNoOp(IModule module)
    {
        var concrete = module.GetType();
        var ifaceMethod = typeof(IModule).GetMethod(
            name: nameof(IModule.CleanDemoDataAsync),
            bindingAttr: System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(TenantDemoSeedContext), typeof(CancellationToken) },
            modifiers: null)
            ?? throw new InvalidOperationException(
                $"Cannot resolve {nameof(IModule)}.{nameof(IModule.CleanDemoDataAsync)}(TenantDemoSeedContext, CancellationToken) — " +
                "the interface signature changed without updating DemoDataCleaner.IsCleanDefaultNoOp.");

        var map = concrete.GetInterfaceMap(typeof(IModule));
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (map.InterfaceMethods[i] != ifaceMethod) continue;
            return map.TargetMethods[i].DeclaringType == typeof(IModule);
        }
        return false;
    }
}
