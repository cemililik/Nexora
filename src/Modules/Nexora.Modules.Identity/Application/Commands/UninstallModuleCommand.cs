using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>
/// Command to uninstall a module for a tenant. When other installed modules
/// declare the target in their <see cref="IModule.Dependencies"/>, the command
/// is rejected unless <see cref="Cascade"/> is <c>true</c> — see ADR-0028 for
/// the dependent-blocking rule and ADR-0031 for the cascade transaction model.
/// </summary>
public sealed record UninstallModuleCommand(
    Guid TenantId,
    string ModuleName,
    bool Cascade = false) : ICommand;

/// <summary>Validates module uninstall input.</summary>
public sealed class UninstallModuleValidator : AbstractValidator<UninstallModuleCommand>
{
    public UninstallModuleValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_id_required");

        RuleFor(x => x.ModuleName)
            .NotEmpty().WithMessage("lockey_identity_validation_module_name_required");
    }
}

/// <summary>
/// Uninstalls a module by:
/// <list type="number">
///   <item><description>Walking the dependency graph; refusing when an installed
///   dependent remains and <see cref="UninstallModuleCommand.Cascade"/> is
///   <c>false</c>.</description></item>
///   <item><description>For each module to remove (single or cascade subtree
///   in reverse-dependency order): calling <c>OnUninstallAsync</c>, removing
///   orphaned role-permissions, renaming module tables to
///   <c>{table}_del_{timestamp}</c>, soft-deleting the <c>TenantModule</c>
///   record, emitting the extended <see cref="ModuleUninstalledIntegrationEvent"/>.</description></item>
///   <item><description>Holding a session-scoped <c>pg_advisory_lock</c>
///   keyed on <c>uninstall:{tenantId}</c> across the full cascade so a
///   concurrent operator cannot interleave a second sequence (per
///   ADR-0031).</description></item>
///   <item><description>On per-module failure: rolling back THAT module only,
///   emitting <see cref="ModuleUninstallFailedIntegrationEvent"/> with the
///   forward log of already-committed modules, throwing
///   <c>Result.Failure</c> with a metadata-bound lockey. Earlier successful modules are NOT
///   auto-undone — operators reinstall within retention to recover.</description></item>
/// </list>
/// </summary>
public sealed class UninstallModuleHandler(
    PlatformDbContext platformDb,
    IdentityDbContext identityDb,
    IEnumerable<IModule> registeredModules,
    IOutbox outbox,
    IUninstallAdvisoryLock advisoryLock,
    ILogger<UninstallModuleHandler> logger) : ICommandHandler<UninstallModuleCommand>
{
    public async Task<Result> Handle(
        UninstallModuleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenantModule = await platformDb.TenantModules
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.ModuleName == request.ModuleName,
                cancellationToken);

        if (tenantModule is null)
        {
            logger.LogWarning("Module {ModuleName} is not installed for tenant {TenantId}", request.ModuleName, request.TenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_not_installed"));
        }

        // Build the installed-module set (intersection of registered modules
        // and currently-installed TenantModule rows for this tenant). The
        // dependency walk uses this set so transitive dependents that are
        // NOT installed don't show up as blockers.
        var installedNames = await platformDb.TenantModules
            .Where(tm => tm.TenantId == tenantId)
            .Select(tm => tm.ModuleName)
            .ToListAsync(cancellationToken);
        var installedNameSet = installedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var registeredList = registeredModules.ToList();
        var installedModules = registeredList
            .Where(m => installedNameSet.Contains(m.Name))
            .ToList();

        var dependents = ModuleDependencyGraph.FindInstalledDependents(
            request.ModuleName, installedModules);

        if (dependents.Count > 0 && !request.Cascade)
        {
            // Non-cascade uninstall with dependents present: refuse without
            // doing any work. Caller must retry with Cascade=true.
            var dependentNames = string.Join(", ", dependents.Select(d => d.Name));
            logger.LogWarning(
                "Uninstall blocked for tenant {TenantId} module {ModuleName}: installed dependents [{Dependents}]",
                request.TenantId, request.ModuleName, dependentNames);
            return Result.Failure(LocalizedMessage.Of(
                "lockey_identity_error_module_uninstall_blocked_by_dependent",
                // Placeholder names follow the camelCase convention used by
                // every other lockey in identity.json (review #07).
                new Dictionary<string, string>
                {
                    ["dependents"] = dependentNames,
                }));
        }

        // Lock spans the whole cascade per ADR-0031. For a single-module
        // uninstall (no dependents) the cascade is just one step but we
        // still take the lock so two operators can't both uninstall the
        // same module concurrently.
        await using var lockHandle = await advisoryLock.AcquireAsync(request.TenantId, cancellationToken);
        if (lockHandle is null)
        {
            logger.LogWarning(
                "Uninstall: another cascade is in flight for tenant {TenantId}; operator should retry shortly.",
                request.TenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_uninstall_lock_busy"));
        }

        var sequence = ModuleDependencyGraph.ReverseUninstallOrder(
            request.ModuleName, installedModules);

        // Forward log: modules whose per-module SaveChanges already committed.
        // On failure at module N, the log is broadcast in the
        // ModuleUninstallFailedIntegrationEvent — it is NOT rolled back per
        // ADR-0031.
        var successes = new List<string>(sequence.Count);

        foreach (var module in sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await UninstallSingleModuleAsync(tenantId, module, cancellationToken);
                successes.Add(module.Name);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation IS an unexpected unwind — let it bubble.
                // The lock handle's `await using` releases the advisory
                // lock so a retry isn't blocked.
                throw;
            }
            // Two narrow catches → one unified Result.Failure path. The
            // per-module exception families that bubble up here are
            // expected operational failures (renamed-table conflict, FK
            // dependency, transient connection blip, model misuse). Per
            // CLAUDE.md "expected failures use Result, unexpected use
            // exceptions" + the standing handler contract that returns
            // Result instead of throwing CascadePartialFailure (review
            // #00 / #46 / #27). Earlier modules in the forward log
            // remain uninstalled per ADR-0031 — the rename-to-_del_
            // is the compensation primitive; the failure event makes
            // the partial state observable to the operator.
            catch (System.Data.Common.DbException ex)
            {
                return await EmitCascadeFailureResultAsync(
                    request, module.Name,
                    "lockey_identity_error_module_uninstall_db_failure",
                    successes, ex, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return await EmitCascadeFailureResultAsync(
                    request, module.Name,
                    "lockey_identity_error_module_uninstall_invalid_state",
                    successes, ex, cancellationToken);
            }
        }

        logger.LogInformation(
            "Uninstall succeeded for tenant {TenantId} module {ModuleName} (cascade subtree: {Modules})",
            request.TenantId, request.ModuleName, string.Join(", ", successes));

        return Result.Success(LocalizedMessage.Of("lockey_identity_module_uninstalled"));
    }

    private async Task<Result> EmitCascadeFailureResultAsync(
        UninstallModuleCommand request,
        string failedModuleName,
        string errorLockey,
        IReadOnlyList<string> successes,
        Exception cause,
        CancellationToken cancellationToken)
    {
        await EmitCascadeFailureAsync(request, failedModuleName, errorLockey, successes, cancellationToken);
        // Lockey carries the failed module name + the forward log so the
        // admin UI can render "uninstall partially completed: X, Y were
        // removed; Z failed" without inspecting an exception payload.
        var meta = new Dictionary<string, string>
        {
            ["failedModule"] = failedModuleName,
            ["successfulModules"] = string.Join(", ", successes),
            ["targetModule"] = request.ModuleName,
        };
        // Log the underlying cause at Error so operators see the stack
        // trace; the Result carries only the lockey + bound metadata.
        logger.LogError(cause,
            "Cascade uninstall failure surfaced as Result.Failure for tenant {TenantId} target {Target} at module {Failed}",
            request.TenantId, request.ModuleName, failedModuleName);
        return Result.Failure(LocalizedMessage.Of(errorLockey, meta));
    }

    /// <summary>
    /// Per-module uninstall step: wraps the rename DDL + Remove + outbox
    /// stage + SaveChanges in a single EF transaction so a partial-rename
    /// failure rolls back ALL renames for this module's loop (review
    /// round-2 zombie-tables finding). Per ADR-0031 the cascade does NOT
    /// wrap these in a single shared transaction across modules — each
    /// module is its own atomic unit.
    /// </summary>
    private async Task UninstallSingleModuleAsync(
        TenantId tenantId, IModule module, CancellationToken ct)
    {
        var tenantModule = await platformDb.TenantModules
            .FirstAsync(tm => tm.TenantId == tenantId && tm.ModuleName == module.Name, ct);

        // Canonical platform schema-name format is tenant_{guid:D} (with
        // hyphens) — set in CreateTenantCommand and TenantContext.
        var schemaName = $"tenant_{tenantId.Value}";
        await module.OnUninstallAsync(
            new TenantInstallContext(tenantId.Value.ToString(), schemaName, null), ct);

        // Remove orphaned role-permission associations for this module.
        // identityDb is a separate DbContext from platformDb so this
        // SaveChanges commits independently — acceptable because the
        // worst case (platform-side rollback after this commits) leaves
        // an over-pruned permission set that gets re-seeded on
        // reinstall, no security impact.
        var modulePermissionIds = await identityDb.Permissions
            .Where(p => p.Module == module.Name)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (modulePermissionIds.Count > 0)
        {
            var orphanedRolePermissions = await identityDb.RolePermissions
                .Where(rp => modulePermissionIds.Contains(rp.PermissionId))
                .ToListAsync(ct);

            if (orphanedRolePermissions.Count > 0)
            {
                identityDb.RolePermissions.RemoveRange(orphanedRolePermissions);
                logger.LogInformation("Removed {Count} role-permission associations for module {ModuleName}",
                    orphanedRolePermissions.Count, module.Name);
            }

            await identityDb.SaveChangesAsync(ct);
        }

        // Single explicit transaction for the platform-side mutations:
        // DDL renames (autocommit per ALTER TABLE in PostgreSQL would
        // create zombie _del_ tables if the loop fails partway, AND
        // would not roll back if the subsequent SaveChanges fails),
        // TenantModule.Remove, and the staged outbox event are now
        // atomic. EF InMemory ignores the transaction call (returns a
        // no-op) so test paths still work.
        var supportsTx = platformDb.Database.IsRelational();
        var tx = supportsTx
            ? await platformDb.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var (canonicalNames, renamedNames) = await RenameModuleTablesAsync(
                tenantId.Value, module.Name, ct);

            if (renamedNames.Count > 0)
            {
                tenantModule.RecordUninstall(string.Join(",", renamedNames));
                logger.LogInformation("Renamed {Count} tables for module {ModuleName} in tenant {TenantId}: {Tables}",
                    renamedNames.Count, module.Name, tenantId.Value, string.Join(", ", renamedNames));
            }

            platformDb.TenantModules.Remove(tenantModule);

            // OutboxService.EnqueueAsync stages the row WITHOUT calling
            // SaveChanges — it relies on the caller's unit-of-work to
            // persist both the domain mutation and the OutboxMessage
            // atomically. SaveChanges below commits both inside the
            // explicit transaction.
            await outbox.EnqueueAsync(new ModuleUninstalledIntegrationEvent
            {
                TenantId = tenantId.Value.ToString(),
                ModuleName = module.Name,
                TenantIdGuid = tenantId.Value,
                CanonicalTableNames = canonicalNames,
                RenamedTableNames = renamedNames,
                // OccurredAt comes from IntegrationEventBase.
            }, ct);

            await platformDb.SaveChangesAsync(ct);

            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        logger.LogInformation("Module {ModuleName} uninstalled for tenant {TenantId}", module.Name, tenantId.Value);
    }

    private async Task EmitCascadeFailureAsync(
        UninstallModuleCommand request,
        string failedModuleName,
        string errorLockey,
        IReadOnlyList<string> successesSoFar,
        CancellationToken ct)
    {
        // Clear the change tracker BEFORE staging the failure event so
        // any uncommitted mutations from the failing module step (e.g. a
        // Remove() that was staged but whose SaveChanges threw) do not
        // get re-attempted alongside the failure-event SaveChanges
        // below. The earlier rollback (UninstallSingleModuleAsync's
        // explicit transaction RollbackAsync) handled the database
        // side; Clear() handles the EF tracker side so SaveChanges only
        // commits the new OutboxMessage row (review round-2 finding —
        // tracker leak risk).
        platformDb.ChangeTracker.Clear();

        await outbox.EnqueueAsync(new ModuleUninstallFailedIntegrationEvent
        {
            TenantId = request.TenantId.ToString(),
            TenantIdGuid = request.TenantId,
            TargetModuleName = request.ModuleName,
            FailedModuleName = failedModuleName,
            ErrorLockey = errorLockey,
            SuccessfulModulesSoFar = successesSoFar.ToList(),
            // OccurredAt comes from IntegrationEventBase.
        }, ct);

        // SaveChanges so the failure-event row reaches the outbox table.
        // After Clear() above, the tracker only contains this freshly
        // staged OutboxMessage; SaveChanges commits exactly that, no
        // domain mutations ride along.
        await platformDb.SaveChangesAsync(ct);

        logger.LogError(
            "Cascade uninstall FAILED for tenant {TenantId} target {Target} at module {Failed}. " +
            "Forward log (committed): [{Successes}].",
            request.TenantId, request.ModuleName, failedModuleName,
            string.Join(", ", successesSoFar));
    }

    /// <summary>
    /// Renames module tables in the tenant schema by appending
    /// <c>_del_{timestamp}</c> and returns (canonical, renamed) pairs.
    /// </summary>
    private async Task<(IReadOnlyList<string> Canonical, IReadOnlyList<string> Renamed)> RenameModuleTablesAsync(
        Guid tenantId, string moduleName, CancellationToken ct)
    {
        var registeredNames = registeredModules.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!registeredNames.Contains(moduleName) && !ModuleNameRegex.IsMatch(moduleName))
        {
            logger.LogWarning("Invalid module name rejected: {ModuleName}", moduleName);
            return ([], []);
        }

        // EF InMemory: schema rename is meaningless. Skip cleanly so tests
        // exercise the rest of the path. Production ALWAYS lands in the
        // relational branch.
        if (!platformDb.Database.IsRelational())
            return ([], []);

        var schemaName = $"tenant_{tenantId}"; // canonical D-format, see comment in UninstallSingleModuleAsync
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var prefix = $"{moduleName}\\_%";
        var canonical = new List<string>();
        var renamed = new List<string>();

        // EF owns the DbContext's connection — borrow it WITHOUT
        // `await using`. Disposing the borrowed connection (or closing it
        // if EF had it open) returns it to the pool prematurely and
        // poisons the next EF call on the same DbContext (review #49).
        var connection = platformDb.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);

        // Enlist every command in the ambient EF transaction (opened by
        // UninstallSingleModuleAsync). Without this, ALTER TABLE would
        // auto-commit per statement and a partial-loop failure would
        // leave zombie _del_ tables that no subsequent retry can find
        // (the discovery query filters out names already containing
        // `_del_`) — review round-2 ORTA finding.
        var ambientTx = platformDb.Database.CurrentTransaction?.GetDbTransaction();

        try
        {
            var tableNames = new List<string>();
            await using (var cmd = connection.CreateCommand())
            {
                if (ambientTx is not null) cmd.Transaction = ambientTx;
                // ESCAPE on BOTH LIKE clauses — the canonical-prefix LIKE
                // also needs the literal `_` escape; without it `auth_%`
                // would match `authX`, `auth_X`, etc., and modules whose
                // names share a prefix could cross-bleed.
                cmd.CommandText = @"
                    SELECT table_name FROM information_schema.tables
                    WHERE table_schema = @schemaName
                    AND table_name LIKE @prefix ESCAPE '\'
                    AND table_name NOT LIKE '%\_del\_%' ESCAPE '\'
                    ORDER BY table_name";

                var schemaParam = cmd.CreateParameter();
                schemaParam.ParameterName = "@schemaName";
                schemaParam.Value = schemaName;
                cmd.Parameters.Add(schemaParam);

                var prefixParam = cmd.CreateParameter();
                prefixParam.ParameterName = "@prefix";
                prefixParam.Value = prefix;
                cmd.Parameters.Add(prefixParam);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    tableNames.Add(reader.GetString(0));
            }

            foreach (var tableName in tableNames)
            {
                var newName = $"{tableName}_del_{timestamp}";
                var renameSQL = $"ALTER TABLE \"{schemaName}\".\"{tableName}\" RENAME TO \"{newName}\"";

                await using var renameCmd = connection.CreateCommand();
                if (ambientTx is not null) renameCmd.Transaction = ambientTx;
                renameCmd.CommandText = renameSQL;
                await renameCmd.ExecuteNonQueryAsync(ct);

                canonical.Add(tableName);
                renamed.Add(newName);
            }
        }
        // No NpgsqlException catch here: a rename failure means the
        // tenant schema is in a state we did not expect (locked table,
        // permissions error, network blip). Letting the exception bubble
        // is correct: the caller's transaction rolls back every rename
        // already performed in this loop, the cascade orchestrator
        // catches DbException at the per-module loop boundary, emits
        // ModuleUninstallFailedIntegrationEvent, and the row stays
        // installed for retry.
        finally
        {
            // Mirror the borrow: only close if we opened. Disposing is
            // never our right to do — EF owns the connection lifetime.
            if (openedHere) await connection.CloseAsync();
        }

        return (canonical, renamed);
    }

    private static readonly Regex ModuleNameRegex = new(
        "^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
}
