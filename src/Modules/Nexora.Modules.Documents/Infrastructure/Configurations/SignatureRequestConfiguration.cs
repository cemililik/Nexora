using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the SignatureRequest entity.</summary>
public sealed class SignatureRequestConfiguration : IEntityTypeConfiguration<SignatureRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SignatureRequest> builder)
    {
        builder.ToTable("documents_signature_requests");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => SignatureRequestId.From(v));
        builder.Property(s => s.DocumentId).HasConversion(id => id.Value, v => DocumentId.From(v));

        builder.Property(s => s.Title).HasMaxLength(500).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.HasIndex(s => new { s.TenantId, s.DocumentId });

        builder.HasMany(s => s.Recipients).WithOne().HasForeignKey(r => r.RequestId);
        builder.Navigation(s => s.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
