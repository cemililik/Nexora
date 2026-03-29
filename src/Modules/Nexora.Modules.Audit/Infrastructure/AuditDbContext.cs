using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Infrastructure;

/// <summary>EF Core database context for the Audit module.</summary>
public sealed class AuditDbContext(
    DbContextOptions<AuditDbContext> options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null)
    : BaseDbContext(options, tenantContextAccessor, domainEventDispatcher)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<AuditSetting> AuditSettings => Set<AuditSetting>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
    }
}
