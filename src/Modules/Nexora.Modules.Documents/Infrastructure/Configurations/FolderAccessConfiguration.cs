using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the FolderAccess entity.</summary>
public sealed class FolderAccessConfiguration : IEntityTypeConfiguration<FolderAccess>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FolderAccess> builder)
    {
        builder.ToTable("documents_folder_accesses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => FolderAccessId.From(v));
        builder.Property(a => a.FolderId).HasConversion(id => id.Value, v => FolderId.From(v));
        builder.Property(a => a.Permission).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => new { a.FolderId, a.UserId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"UserId\" IS NOT NULL");
        builder.HasIndex(a => new { a.FolderId, a.RoleId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"RoleId\" IS NOT NULL");
        builder.HasIndex(a => a.ExpiresAt).HasFilter("\"ExpiresAt\" IS NOT NULL");
    }
}
