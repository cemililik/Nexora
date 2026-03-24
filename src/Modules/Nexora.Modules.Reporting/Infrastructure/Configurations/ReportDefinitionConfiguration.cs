using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;

namespace Nexora.Modules.Reporting.Infrastructure.Configurations;

public sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("reporting_report_definitions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, v => ReportDefinitionId.From(v));

        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.Module).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Category).HasMaxLength(100);
        builder.Property(d => d.QueryText).IsRequired();
        builder.Property(d => d.Parameters).HasColumnType("jsonb");
        builder.Property(d => d.DefaultFormat).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(d => new { d.TenantId, d.Module });
        builder.HasIndex(d => new { d.TenantId, d.IsActive });
    }
}
