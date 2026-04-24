using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for the RolePermission entity.</summary>
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("identity_role_permissions");
        builder.HasKey(rp => rp.Id);
        builder.Property(rp => rp.Id).HasConversion(id => id.Value, v => RolePermissionId.From(v));
        builder.Property(rp => rp.RoleId).HasConversion(id => id.Value, v => RoleId.From(v));
        builder.Property(rp => rp.PermissionId).HasConversion(id => id.Value, v => PermissionId.From(v));
        // T-022: no HasFilter — RolePermission extends Entity<T>, not
        // AuditableEntity<T>, so no IsDeleted column exists on this join. Role
        // ↔ Permission links are hard-deleted when a permission is revoked.
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
    }
}
