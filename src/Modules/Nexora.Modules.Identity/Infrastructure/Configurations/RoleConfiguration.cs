using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Role entity.</summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("identity_roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => RoleId.From(v));
        builder.Property(r => r.TenantId).HasConversion(id => id.Value, v => TenantId.From(v));

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
        builder.Property(r => r.Description).HasMaxLength(500);

        builder.HasMany(r => r.Permissions).WithOne().HasForeignKey(rp => rp.RoleId);
        builder.Navigation(r => r.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
