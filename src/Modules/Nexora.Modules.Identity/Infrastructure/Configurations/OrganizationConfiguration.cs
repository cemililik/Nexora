using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("identity_organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasConversion(id => id.Value, v => OrganizationId.From(v));
        builder.Property(o => o.TenantId).HasConversion(id => id.Value, v => TenantId.From(v));

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(o => new { o.TenantId, o.Slug }).IsUnique();
        builder.Property(o => o.LogoUrl).HasMaxLength(500);
        builder.Property(o => o.Timezone).HasMaxLength(50);
        builder.Property(o => o.DefaultCurrency).HasMaxLength(3);
        builder.Property(o => o.DefaultLanguage).HasMaxLength(10);

        builder.HasMany(o => o.Departments).WithOne().HasForeignKey(d => d.OrganizationId);
        builder.Navigation(o => o.Departments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
