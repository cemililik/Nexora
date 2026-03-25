using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;

namespace Nexora.Modules.Reporting.Infrastructure.Configurations;

public sealed class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> builder)
    {
        builder.ToTable("reporting_dashboards");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, v => DashboardId.From(v));

        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.Widgets).HasColumnType("jsonb");

        builder.HasIndex(d => new { d.TenantId, d.OrganizationId });
    }
}
