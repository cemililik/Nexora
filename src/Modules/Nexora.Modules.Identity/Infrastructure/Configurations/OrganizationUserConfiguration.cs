using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

public sealed class OrganizationUserConfiguration : IEntityTypeConfiguration<OrganizationUser>
{
    public void Configure(EntityTypeBuilder<OrganizationUser> builder)
    {
        builder.ToTable("identity_organization_users");
        builder.HasKey(ou => ou.Id);
        builder.Property(ou => ou.Id).HasConversion(id => id.Value, v => OrganizationUserId.From(v));
        builder.Property(ou => ou.UserId).HasConversion(id => id.Value, v => UserId.From(v));
        builder.Property(ou => ou.OrganizationId).HasConversion(id => id.Value, v => OrganizationId.From(v));
        builder.HasIndex(ou => new { ou.UserId, ou.OrganizationId }).IsUnique();

        builder.HasMany(ou => ou.UserRoles).WithOne().HasForeignKey(ur => ur.OrganizationUserId);
        builder.Navigation(ou => ou.UserRoles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
