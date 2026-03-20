using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Permission entity.</summary>
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("identity_permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, v => PermissionId.From(v));

        builder.Property(p => p.Module).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Resource).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Action).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => new { p.Module, p.Resource, p.Action }).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(500);

        builder.Ignore(p => p.Key);
    }
}
