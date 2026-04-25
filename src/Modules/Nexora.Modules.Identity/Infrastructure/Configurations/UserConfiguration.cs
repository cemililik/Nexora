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
        builder.Property(u => u.PreferredLanguage).HasMaxLength(10);

        // Optional link to Contacts module (raw Guid — crosses module boundary).
        // Partial unique index: a contact may be linked to at most one user per tenant
        // (NULL rows are excluded so unlinked users don't collide with each other).
        builder.Property(u => u.ContactId).IsRequired(false);
        // Partial unique index: one contact may be linked to at most one user
        // per tenant schema (NULL rows excluded so unlinked users never collide).
        // Uniqueness is schema-scoped — cross-tenant isolation is enforced by
        // PostgreSQL's schema-per-tenant model, not by this index.
        builder.HasIndex(u => u.ContactId)
            .IsUnique()
            .HasFilter("\"ContactId\" IS NOT NULL")
            .HasDatabaseName("ix_identity_users_contact_id_unique");

        builder.HasMany(u => u.OrganizationUsers).WithOne().HasForeignKey(ou => ou.UserId);
        builder.Navigation(u => u.OrganizationUsers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.FullName);
    }
}
