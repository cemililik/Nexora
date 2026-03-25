using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for the User entity.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("identity_users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasConversion(id => id.Value, v => UserId.From(v));
        builder.Property(u => u.TenantId).HasConversion(id => id.Value, v => TenantId.From(v));

        builder.Property(u => u.KeycloakUserId).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.KeycloakUserId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(30);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasMany(u => u.OrganizationUsers).WithOne().HasForeignKey(ou => ou.UserId);
        builder.Navigation(u => u.OrganizationUsers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.FullName);
    }
}
