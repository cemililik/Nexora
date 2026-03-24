using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;

namespace Nexora.Modules.Reporting.Infrastructure.Configurations;

public sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("reporting_report_schedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => ReportScheduleId.From(v));
        builder.Property(s => s.DefinitionId).HasConversion(id => id.Value, v => ReportDefinitionId.From(v));

        builder.Property(s => s.CronExpression).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Format).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Recipients).HasColumnType("jsonb");

        builder.HasIndex(s => new { s.TenantId, s.IsActive });
    }
}
