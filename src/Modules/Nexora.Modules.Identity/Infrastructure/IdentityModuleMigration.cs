using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Applies Identity module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class IdentityModuleMigration(
    IServiceProvider serviceProvider,
    ILogger<IdentityModuleMigration> logger) : IModuleMigration
{
    public string ModuleName => "identity";

    /// <inheritdoc />
    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new IdentityDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);

        logger.LogInformation("Identity module migration applied for schema {SchemaName}", schemaName);
    }

    /// <inheritdoc />
    public async Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenantId = ExtractTenantId(schemaName);
        accessor.SetTenant(tenantId);

        // ADR-004 / permissions.md §3 — the registry is the single source of truth. Each
        // module populated it during IModule.OnStartupAsync; we now reconcile those
        // declarations with the per-tenant identity_permissions table idempotently.
        var registry = scope.ServiceProvider.GetRequiredService<IPermissionRegistry>();
        var declared = registry.GetAll();

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new IdentityDbContext(options, accessor);

        // 1) Add any declared permission that is missing in this tenant schema.
        var existing = await dbContext.Permissions
            .Select(p => new { p.Module, p.Resource, p.Action, p.Scope, p.Id })
            .ToListAsync(ct);
        var existingByName = existing.ToDictionary(
            p => $"{p.Module}.{p.Resource}.{p.Action}",
            p => (p.Id, p.Scope));

        var toAdd = declared
            .Where(def => !existingByName.ContainsKey(def.Name))
            .Select(def => Permission.Create(def.Module, def.Resource, def.Action, def.DescriptionKey, def.Scope))
            .ToList();

        if (toAdd.Count > 0)
        {
            await dbContext.Permissions.AddRangeAsync(toAdd, ct);
            logger.LogInformation(
                "Identity seed: added {Count} new permission(s) to tenant {Tenant} ({Names})",
                toAdd.Count, tenantId, string.Join(", ", toAdd.Select(p => $"{p.Module}.{p.Resource}.{p.Action}")));
        }

        // 2) Idempotent scope reconciliation — if the registry says a permission is
        //    Platform-scope but the DB row says Tenant (legacy state), flip it.
        var declaredByName = declared.ToDictionary(d => d.Name);
        var scopeFixes = await dbContext.Permissions.ToListAsync(ct);
        foreach (var dbPerm in scopeFixes)
        {
            var name = $"{dbPerm.Module}.{dbPerm.Resource}.{dbPerm.Action}";
            if (declaredByName.TryGetValue(name, out var def) && dbPerm.Scope != def.Scope)
            {
                logger.LogInformation(
                    "Identity seed: reclassifying {Name} from {OldScope} to {NewScope} per registry",
                    name, dbPerm.Scope, def.Scope);
                dbPerm.SetScope(def.Scope);
            }
        }

        // 3) Seed the tenant-side "Platform Admin" role on first run OR top it up on every
        //    run with any Tenant-scope permission it doesn't already hold. Platform-scope
        //    permissions are NEVER granted here (permissions.md §2 — enforced at seed time).
        var adminRole = await dbContext.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.IsSystemRole && r.Name == "Platform Admin", ct);

        if (adminRole is null)
        {
            var tenantIdTyped = Domain.ValueObjects.TenantId.Parse(tenantId);
            adminRole = Role.Create(
                tenantIdTyped, "Platform Admin", "lockey_identity_role_platform_admin_description",
                isSystem: true);
            await dbContext.Roles.AddAsync(adminRole, ct);
        }

        // Save first so newly-added permissions get IDs before we assign them.
        await dbContext.SaveChangesAsync(ct);

        var tenantPermissions = await dbContext.Permissions
            .Where(p => p.Scope == PermissionScope.Tenant)
            .ToListAsync(ct);
        var already = adminRole.Permissions.Select(rp => rp.PermissionId).ToHashSet();

        var granted = 0;
        foreach (var perm in tenantPermissions)
        {
            if (!already.Contains(perm.Id))
            {
                adminRole.AssignPermission(perm);
                granted++;
            }
        }

        // 4) Strip any Platform-scope permissions that were accidentally assigned to this
        //    tenant role by legacy seed code — idempotent cleanup of pre-existing drift.
        var platformPermissionIds = await dbContext.Permissions
            .Where(p => p.Scope == PermissionScope.Platform)
            .Select(p => p.Id)
            .ToListAsync(ct);
        var stripped = adminRole.RemovePermissionsByIds(platformPermissionIds);
        if (stripped > 0)
        {
            logger.LogWarning(
                "Identity seed: stripped {Count} Platform-scope permission(s) from tenant role '{Role}' on tenant {Tenant}",
                stripped, adminRole.Name, tenantId);
        }

        if (granted > 0)
        {
            logger.LogInformation(
                "Identity seed: granted {Count} new Tenant-scope permission(s) to Platform Admin role on tenant {Tenant}",
                granted, tenantId);
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Identity module seed completed for schema {SchemaName}", schemaName);
    }

    private DbContextOptions<IdentityDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
