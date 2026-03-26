using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Infrastructure.Persistence;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Reporting.Infrastructure;

public sealed class ReportingDbContext(
    DbContextOptions<ReportingDbContext> options,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ReportingDbContext>? logger = null,
    DomainEventDispatcher? domainEventDispatcher = null)
    : BaseDbContext(options, tenantContextAccessor, domainEventDispatcher)
{
    private readonly ILogger<ReportingDbContext>? _logger = logger;

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
                if (orgId is null) return Guid.Empty;
                if (Guid.TryParse(orgId, out var parsed)) return parsed;
                _logger?.LogWarning("Tenant context contains invalid OrganizationId format: {OrganizationId}", orgId);
                return Guid.Empty;
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning(ex, "Tenant context not available when resolving CurrentOrganizationId");
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
