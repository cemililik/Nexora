using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the ImportJob entity.</summary>
public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("contacts_import_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasConversion(id => id.Value, v => ImportJobId.From(v));

        builder.Property(j => j.FileName).HasMaxLength(500).IsRequired();
        builder.Property(j => j.FileFormat).HasMaxLength(10).IsRequired();
        builder.Property(j => j.StorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.ErrorDetails).HasColumnType("jsonb");
        builder.Property(j => j.HangfireJobId).HasMaxLength(100);
        builder.Property(j => j.CreatedBy).HasMaxLength(200);

        builder.HasIndex(j => new { j.TenantId, j.Status });
        builder.HasIndex(j => new { j.TenantId, j.HangfireJobId });
    }
}
