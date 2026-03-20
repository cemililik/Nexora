using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Tenant entity.</summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("identity_tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => TenantId.From(v));

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.RealmId).HasMaxLength(200);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Settings).HasColumnType("jsonb");

        builder.HasMany(t => t.Organizations).WithOne().HasForeignKey(o => o.TenantId);
        builder.HasMany(t => t.Modules).WithOne().HasForeignKey(m => m.TenantId);

        builder.Navigation(t => t.Organizations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Modules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
