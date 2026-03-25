using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to permanently uninstall a module for a tenant. Renames tables and soft-deletes the record.</summary>
public sealed record UninstallModuleCommand(
    Guid TenantId,
    string ModuleName) : ICommand;

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
/// 1. Calling module's OnUninstallAsync callback
/// 2. Removing orphaned role-permission associations
/// 3. Renaming module tables with _del_{timestamp} suffix
/// 4. Recording renamed table names
/// 5. Soft-deleting the TenantModule record (IsDeleted=true)
/// </summary>
public sealed class UninstallModuleHandler(
    PlatformDbContext platformDb,
    IdentityDbContext identityDb,
    IEnumerable<IModule> registeredModules,
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

        // Call module's OnUninstallAsync if available
        var module = registeredModules.FirstOrDefault(m => m.Name == request.ModuleName);
        if (module is not null)
        {
            var schemaName = $"tenant_{tenantId.Value}";
            await module.OnUninstallAsync(
                new TenantInstallContext(tenantId.Value.ToString(), schemaName, null),
                cancellationToken);
        }

        // Remove role-permission associations for the uninstalled module's permissions
        var modulePermissionIds = await identityDb.Permissions
            .Where(p => p.Module == request.ModuleName)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (modulePermissionIds.Count > 0)
        {
            var orphanedRolePermissions = await identityDb.RolePermissions
                .Where(rp => modulePermissionIds.Contains(rp.PermissionId))
                .ToListAsync(cancellationToken);

            if (orphanedRolePermissions.Count > 0)
            {
                identityDb.RolePermissions.RemoveRange(orphanedRolePermissions);
                logger.LogInformation("Removed {Count} role-permission associations for module {ModuleName}",
                    orphanedRolePermissions.Count, request.ModuleName);
            }

            await identityDb.SaveChangesAsync(cancellationToken);
        }

        // Rename module tables in tenant schema with _del_{timestamp} suffix
        var renamedTables = await RenameModuleTablesAsync(
            request.TenantId, request.ModuleName, cancellationToken);

        if (renamedTables.Count > 0)
        {
            tenantModule.RecordUninstall(string.Join(",", renamedTables));
            logger.LogInformation("Renamed {Count} tables for module {ModuleName} in tenant {TenantId}: {Tables}",
                renamedTables.Count, request.ModuleName, request.TenantId, string.Join(", ", renamedTables));
        }

        // Soft delete the TenantModule record
        platformDb.TenantModules.Remove(tenantModule);
        await platformDb.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Module {ModuleName} uninstalled for tenant {TenantId}", request.ModuleName, request.TenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_module_uninstalled"));
    }

    /// <summary>
    /// Renames module tables in the tenant schema by appending _del_{timestamp}.
    /// Returns the list of new table names.
    /// </summary>
    private async Task<List<string>> RenameModuleTablesAsync(
        Guid tenantId, string moduleName, CancellationToken ct)
    {
        // Validate moduleName against registered module names to prevent SQL injection
        var registeredNames = registeredModules.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!registeredNames.Contains(moduleName) && !Regex.IsMatch(moduleName, "^[a-z][a-z0-9_]*$"))
        {
            logger.LogWarning("Invalid module name rejected: {ModuleName}", moduleName);
            return [];
        }

        var schemaName = $"tenant_{tenantId:N}";
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var prefix = $"{moduleName}\\_%";
        var renamedTables = new List<string>();

        try
        {
            await using var connection = platformDb.Database.GetDbConnection();
            await connection.OpenAsync(ct);

            // Find all tables in the tenant schema that belong to this module using parameterized query
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

            // Rename each table (table/schema names can't be parameterized in PostgreSQL,
            // but moduleName is validated above against registered modules or regex whitelist)
            foreach (var tableName in tableNames)
            {
                var newName = $"{tableName}_del_{timestamp}";
                var renameSQL = $"ALTER TABLE \"{schemaName}\".\"{tableName}\" RENAME TO \"{newName}\"";

                await using var renameCmd = connection.CreateCommand();
                renameCmd.CommandText = renameSQL;
                await renameCmd.ExecuteNonQueryAsync(ct);

                renamedTables.Add(newName);
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

        return renamedTables;
    }
}
