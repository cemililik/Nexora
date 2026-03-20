using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Configurations;

/// <summary>EF Core configuration for the SignatureRecipient entity.</summary>
public sealed class SignatureRecipientConfiguration : IEntityTypeConfiguration<SignatureRecipient>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SignatureRecipient> builder)
    {
        builder.ToTable("documents_signature_recipients");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => SignatureRecipientId.From(v));
        builder.Property(r => r.RequestId).HasConversion(id => id.Value, v => SignatureRequestId.From(v));

        builder.Property(r => r.Email).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.IpAddress).HasMaxLength(45);

        builder.HasIndex(r => r.RequestId);
    }
}
