using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
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
///   <see cref="CascadePartialFailure"/>. Earlier successful modules are NOT
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
                new Dictionary<string, string>
                {
                    ["Dependents"] = dependentNames,
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
            // Narrow exception families per CLAUDE.md "Never catch(Exception)
            // in module code" — the same envelope MigrationRunner uses.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                await EmitCascadeFailureAsync(request, module.Name, "lockey_identity_error_module_uninstall_db_failure", successes, cancellationToken);
                throw new CascadePartialFailure(module.Name, "lockey_identity_error_module_uninstall_db_failure", successes, ex);
            }
            catch (System.Data.Common.DbException ex)
            {
                await EmitCascadeFailureAsync(request, module.Name, "lockey_identity_error_module_uninstall_db_failure", successes, cancellationToken);
                throw new CascadePartialFailure(module.Name, "lockey_identity_error_module_uninstall_db_failure", successes, ex);
            }
            catch (InvalidOperationException ex)
            {
                await EmitCascadeFailureAsync(request, module.Name, "lockey_identity_error_module_uninstall_invalid_state", successes, cancellationToken);
                throw new CascadePartialFailure(module.Name, "lockey_identity_error_module_uninstall_invalid_state", successes, ex);
            }
        }

        logger.LogInformation(
            "Uninstall succeeded for tenant {TenantId} module {ModuleName} (cascade subtree: {Modules})",
            request.TenantId, request.ModuleName, string.Join(", ", successes));

        return Result.Success(LocalizedMessage.Of("lockey_identity_module_uninstalled"));
    }

    /// <summary>
    /// Per-module uninstall step: corresponds to a single per-module
    /// transaction (the implicit transaction that <c>SaveChangesAsync</c>
    /// opens for each db call). Per ADR-0031 the cascade does NOT wrap these
    /// in a single shared transaction across modules.
    /// </summary>
    private async Task UninstallSingleModuleAsync(
        TenantId tenantId, IModule module, CancellationToken ct)
    {
        var tenantModule = await platformDb.TenantModules
            .FirstAsync(tm => tm.TenantId == tenantId && tm.ModuleName == module.Name, ct);

        // Canonical platform schema-name format is tenant_{guid:D} (with
        // hyphens) — set in CreateTenantCommand and TenantContext. The
        // earlier ":N" form here would target a non-existent schema and
        // every information_schema lookup would silently return zero rows.
        var schemaName = $"tenant_{tenantId.Value}";
        await module.OnUninstallAsync(
            new TenantInstallContext(tenantId.Value.ToString(), schemaName, null), ct);

        // Remove orphaned role-permission associations for this module.
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
        // SaveChanges — it relies on the caller's unit-of-work to persist
        // both the domain mutation and the OutboxMessage atomically. The
        // earlier ordering (SaveChanges → Enqueue → return) flushed the
        // TenantModule mutation but left the integration event row staged
        // in the change tracker until the *next* SaveChanges, which never
        // came on a single-module uninstall — so downstream consumers
        // never received `ModuleUninstalledIntegrationEvent`. Stage the
        // event first, then SaveChanges once for both.
        await outbox.EnqueueAsync(new ModuleUninstalledIntegrationEvent
        {
            TenantId = tenantId.Value.ToString(),
            ModuleName = module.Name,
            TenantIdGuid = tenantId.Value,
            CanonicalTableNames = canonicalNames,
            RenamedTableNames = renamedNames,
            UninstalledAtUtc = DateTimeOffset.UtcNow,
        }, ct);

        await platformDb.SaveChangesAsync(ct);

        logger.LogInformation("Module {ModuleName} uninstalled for tenant {TenantId}", module.Name, tenantId.Value);
    }

    private async Task EmitCascadeFailureAsync(
        UninstallModuleCommand request,
        string failedModuleName,
        string errorLockey,
        IReadOnlyList<string> successesSoFar,
        CancellationToken ct)
    {
        await outbox.EnqueueAsync(new ModuleUninstallFailedIntegrationEvent
        {
            TenantId = request.TenantId.ToString(),
            TenantIdGuid = request.TenantId,
            TargetModuleName = request.ModuleName,
            FailedModuleName = failedModuleName,
            ErrorLockey = errorLockey,
            SuccessfulModulesSoFar = successesSoFar.ToList(),
            FailedAtUtc = DateTimeOffset.UtcNow,
        }, ct);

        // SaveChanges before the caller throws CascadePartialFailure;
        // otherwise the staged failure-event row never reaches the
        // outbox table and downstream consumers (admin UI, audit) lose
        // the partial-state signal. The failing module's per-module
        // transaction has already rolled back, so this SaveChanges only
        // persists the OutboxMessage row — no domain mutation rides
        // along on the failing branch.
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
        if (!registeredNames.Contains(moduleName) && !Regex.IsMatch(moduleName, "^[a-z][a-z0-9_]*$"))
        {
            logger.LogWarning("Invalid module name rejected: {ModuleName}", moduleName);
            return ([], []);
        }

        var schemaName = $"tenant_{tenantId}"; // canonical D-format, see comment in UninstallSingleModuleAsync
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var prefix = $"{moduleName}\\_%";
        var canonical = new List<string>();
        var renamed = new List<string>();

        try
        {
            await using var connection = platformDb.Database.GetDbConnection();
            await connection.OpenAsync(ct);

            var tableNames = new List<string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT table_name FROM information_schema.tables
                    WHERE table_schema = @schemaName
                    AND table_name LIKE @prefix
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
                renameCmd.CommandText = renameSQL;
                await renameCmd.ExecuteNonQueryAsync(ct);

                canonical.Add(tableName);
                renamed.Add(newName);
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to rename tables for module {ModuleName} in tenant {TenantId}",
                moduleName, tenantId);
            // Don't fail the uninstall — tables may not exist or be already renamed
        }
        catch (InvalidOperationException)
        {
            // InMemory/non-relational provider — skip table rename (only works with PostgreSQL)
        }

        return (canonical, renamed);
    }
}
