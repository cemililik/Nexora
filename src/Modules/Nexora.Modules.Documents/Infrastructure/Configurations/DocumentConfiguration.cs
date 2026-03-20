using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Document entity.</summary>
public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents_documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, v => DocumentId.From(v));
        builder.Property(d => d.FolderId).HasConversion(id => id.Value, v => FolderId.From(v));

        builder.Property(d => d.Name).HasMaxLength(500).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.LinkedEntityType).HasMaxLength(100);
        // Tags are modeled as a comma-separated string, so map to a simple string column instead of jsonb.
        builder.Property(d => d.Tags).HasMaxLength(2000);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(d => new { d.TenantId, d.FolderId });
        builder.HasIndex(d => new { d.TenantId, d.Status });
        builder.HasIndex(d => new { d.TenantId, d.LinkedEntityId });
        builder.HasIndex(d => new { d.TenantId, d.Name });

        builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId);
        builder.Navigation(d => d.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(d => d.AccessList).WithOne().HasForeignKey(a => a.DocumentId);
        builder.Navigation(d => d.AccessList).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
