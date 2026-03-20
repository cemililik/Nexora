using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for all module contexts. Provides schema-per-tenant support,
/// audit fields, and domain event dispatching.
/// </summary>
public abstract class BaseDbContext(
    DbContextOptions options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null) : DbContext(options), IUnitOfWork
{
    protected ITenantContextAccessor TenantContextAccessor { get; } = tenantContextAccessor;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        try
        {
            var schema = TenantContextAccessor.Current.SchemaName;
            if (!string.IsNullOrEmpty(schema))
                modelBuilder.HasDefaultSchema(schema);
        }
        catch (InvalidOperationException)
        {
            // Tenant context not set — used during migrations or design-time
            // Schema will be applied at runtime via connection
        }
    }

    /// <summary>Saves changes, sets audit fields, and dispatches domain events.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SetAuditFields();
        var result = await base.SaveChangesAsync(ct);

        if (domainEventDispatcher is not null)
            await domainEventDispatcher.DispatchEventsAsync(this, ct);

        return result;
    }

    private void SetAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        string? userId = null;

        try
        {
            userId = TenantContextAccessor.Current.UserId;
        }
        catch (InvalidOperationException)
        {
            // Tenant context not available (e.g., during provisioning)
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            var entityType = entry.Entity.GetType();

            if (!IsAuditableEntity(entityType))
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property("CreatedAt").CurrentValue = now;
                    entry.Property("CreatedBy").CurrentValue = userId;
                    break;
                case EntityState.Modified:
                    entry.Property("UpdatedAt").CurrentValue = now;
                    entry.Property("UpdatedBy").CurrentValue = userId;
                    break;
            }
        }
    }

    private static bool IsAuditableEntity(Type type)
    {
        var current = type;
        while (current != null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AuditableEntity<>))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
