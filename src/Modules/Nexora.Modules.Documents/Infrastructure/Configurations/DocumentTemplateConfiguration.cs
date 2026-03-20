using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the DocumentTemplate entity.</summary>
public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.ToTable("documents_document_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => DocumentTemplateId.From(v));

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Format).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.TemplateStorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(t => t.VariableDefinitions).HasColumnType("jsonb");

        builder.HasIndex(t => new { t.TenantId, t.Category });
        builder.HasIndex(t => new { t.TenantId, t.IsActive });
    }
}
