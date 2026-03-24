using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;

namespace Nexora.Modules.Reporting.Infrastructure.Configurations;

public sealed class ReportExecutionConfiguration : IEntityTypeConfiguration<ReportExecution>
{
    public void Configure(EntityTypeBuilder<ReportExecution> builder)
    {
        builder.ToTable("reporting_report_executions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, v => ReportExecutionId.From(v));
        builder.Property(e => e.DefinitionId).HasConversion(id => id.Value, v => ReportDefinitionId.From(v));

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ParameterValues).HasColumnType("jsonb");
        builder.Property(e => e.ResultStorageKey).HasMaxLength(500);
        builder.Property(e => e.Format).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ErrorDetails).HasMaxLength(4000);
        builder.Property(e => e.ExecutedBy).HasMaxLength(256);
        builder.Property(e => e.HangfireJobId).HasMaxLength(100);

        builder.HasIndex(e => new { e.TenantId, e.DefinitionId });
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}
