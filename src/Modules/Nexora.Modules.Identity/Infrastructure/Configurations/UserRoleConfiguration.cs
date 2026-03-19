using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("identity_user_roles");
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).HasConversion(id => id.Value, v => UserRoleId.From(v));
        builder.Property(ur => ur.OrganizationUserId).HasConversion(id => id.Value, v => OrganizationUserId.From(v));
        builder.Property(ur => ur.RoleId).HasConversion(id => id.Value, v => RoleId.From(v));
        builder.HasIndex(ur => new { ur.OrganizationUserId, ur.RoleId }).IsUnique();
    }
}
