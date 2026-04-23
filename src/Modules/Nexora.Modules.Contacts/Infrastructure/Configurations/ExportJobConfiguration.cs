using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the <see cref="ExportJob"/> entity.</summary>
public sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("contacts_export_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasConversion(id => id.Value, v => ExportJobId.From(v));

        builder.Property(j => j.Format).HasMaxLength(10).IsRequired();
        builder.Property(j => j.StorageKey).HasMaxLength(1000).IsRequired(false);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.ErrorDetails).HasColumnType("jsonb").IsRequired(false);
        builder.Property(j => j.HangfireJobId).HasMaxLength(100);
        builder.Property(j => j.FiltersJson).HasColumnType("text").IsRequired(false);
        builder.Property(j => j.FieldsJson).HasColumnType("text").IsRequired(false);
        builder.Property(j => j.CreatedBy).HasMaxLength(200);

        builder.HasIndex(j => new { j.TenantId, j.Status });
        builder.HasIndex(j => new { j.TenantId, j.HangfireJobId });
    }
}
