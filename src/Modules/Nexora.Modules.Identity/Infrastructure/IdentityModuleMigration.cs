using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Applies Identity module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class IdentityModuleMigration(IServiceProvider serviceProvider) : IModuleMigration
{
    public string ModuleName => "identity";

    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new IdentityDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);
    }

    public async Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenantId = ExtractTenantId(schemaName);
        accessor.SetTenant(tenantId);

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new IdentityDbContext(options, accessor);

        // Seed default permissions if not exist
        if (!await dbContext.Permissions.AnyAsync(ct))
        {
            var defaultPermissions = new[]
            {
                Permission.Create("identity", "tenants", "read", "lockey_identity_permission_tenants_read"),
                Permission.Create("identity", "tenants", "manage", "lockey_identity_permission_tenants_manage"),
                Permission.Create("identity", "organizations", "read", "lockey_identity_permission_organizations_read"),
                Permission.Create("identity", "organizations", "create", "lockey_identity_permission_organizations_create"),
                Permission.Create("identity", "organizations", "update", "lockey_identity_permission_organizations_update"),
                Permission.Create("identity", "organizations", "delete", "lockey_identity_permission_organizations_delete"),
                Permission.Create("identity", "users", "read", "lockey_identity_permission_users_read"),
                Permission.Create("identity", "users", "create", "lockey_identity_permission_users_create"),
                Permission.Create("identity", "users", "update", "lockey_identity_permission_users_update"),
                Permission.Create("identity", "users", "delete", "lockey_identity_permission_users_delete"),
                Permission.Create("identity", "roles", "read", "lockey_identity_permission_roles_read"),
                Permission.Create("identity", "roles", "create", "lockey_identity_permission_roles_create"),
                Permission.Create("identity", "roles", "update", "lockey_identity_permission_roles_update"),
                Permission.Create("identity", "roles", "delete", "lockey_identity_permission_roles_delete"),
                Permission.Create("identity", "modules", "read", "lockey_identity_permission_modules_read"),
                Permission.Create("identity", "modules", "manage", "lockey_identity_permission_modules_manage"),
            };

            await dbContext.Permissions.AddRangeAsync(defaultPermissions, ct);
        }

        // Seed platform-admin role if not exist
        if (!await dbContext.Roles.AnyAsync(r => r.IsSystemRole, ct))
        {
            var tenantIdTyped = Domain.ValueObjects.TenantId.Parse(tenantId);
            var adminRole = Role.Create(tenantIdTyped, "Platform Admin", "lockey_identity_role_platform_admin_description", isSystem: true);

            var allPermissions = await dbContext.Permissions.ToListAsync(ct);
            foreach (var permission in allPermissions)
                adminRole.AssignPermission(permission);

            await dbContext.Roles.AddAsync(adminRole, ct);
        }

        await dbContext.SaveChangesAsync(ct);
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
