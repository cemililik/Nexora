using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("identity_departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, v => DepartmentId.From(v));
        builder.Property(d => d.OrganizationId).HasConversion(id => id.Value, v => OrganizationId.From(v));
        builder.Property(d => d.ParentDepartmentId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? DepartmentId.From(v.Value) : (DepartmentId?)null);
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
    }
}
