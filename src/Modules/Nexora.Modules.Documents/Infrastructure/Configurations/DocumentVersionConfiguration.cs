using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the DocumentVersion entity.</summary>
public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("documents_document_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasConversion(id => id.Value, v => DocumentVersionId.From(v));
        builder.Property(v => v.DocumentId).HasConversion(id => id.Value, v => DocumentId.From(v));

        builder.Property(v => v.StorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(v => v.ChangeNote).HasMaxLength(500);

        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}
