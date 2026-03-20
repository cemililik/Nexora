using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the DocumentAccess entity.</summary>
public sealed class DocumentAccessConfiguration : IEntityTypeConfiguration<DocumentAccess>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DocumentAccess> builder)
    {
        builder.ToTable("documents_document_accesses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => DocumentAccessId.From(v));
        builder.Property(a => a.DocumentId).HasConversion(id => id.Value, v => DocumentId.From(v));
        builder.Property(a => a.Permission).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => new { a.DocumentId, a.UserId });
        builder.HasIndex(a => new { a.DocumentId, a.RoleId });
    }
}
