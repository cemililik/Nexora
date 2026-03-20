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
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
    }
}
