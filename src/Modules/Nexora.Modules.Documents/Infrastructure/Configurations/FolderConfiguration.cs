using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Folder entity.</summary>
public sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("documents_folders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasConversion(id => id.Value, v => FolderId.From(v));
        builder.Property(f => f.ParentFolderId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? FolderId.From(v.Value) : null);

        builder.Property(f => f.Name).HasMaxLength(255).IsRequired();
        builder.Property(f => f.Path).HasMaxLength(1000).IsRequired();
        builder.Property(f => f.ModuleName).HasMaxLength(50);

        builder.HasIndex(f => new { f.TenantId, f.ParentFolderId });
        builder.HasIndex(f => new { f.TenantId, f.ModuleName });
        builder.HasIndex(f => new { f.TenantId, f.Path });
    }
}
