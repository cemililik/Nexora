using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Reporting.Infrastructure;

public sealed class ReportingDbContext(
    DbContextOptions<ReportingDbContext> options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null)
    : BaseDbContext(options, tenantContextAccessor, domainEventDispatcher)
{
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportExecution> ReportExecutions => Set<ReportExecution>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();

    /// <summary>
    /// Gets the current OrganizationId from the tenant context.
    /// Used by EF Core global query filters to enforce org-level isolation.
    /// </summary>
    private Guid CurrentOrganizationId
    {
        get
        {
            try
            {
                var orgId = TenantContextAccessor.Current.OrganizationId;
                return orgId is not null ? Guid.Parse(orgId) : Guid.Empty;
            }
            catch (InvalidOperationException)
            {
                return Guid.Empty;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
        ApplyOrganizationQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for OrganizationId on entities that support org-level isolation.
    /// Combines with soft delete filter so both are enforced simultaneously.
    /// </summary>
    private void ApplyOrganizationQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportDefinition>(e =>
            e.HasQueryFilter(r => !r.IsDeleted && r.OrganizationId == CurrentOrganizationId));

        modelBuilder.Entity<Dashboard>(e =>
            e.HasQueryFilter(d => !d.IsDeleted && d.OrganizationId == CurrentOrganizationId));
    }
}
