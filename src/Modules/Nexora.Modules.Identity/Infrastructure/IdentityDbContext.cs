using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>Tenant-scoped DbContext for the Identity module.</summary>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null)
    : BaseDbContext(options, tenantContextAccessor, domainEventDispatcher)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
