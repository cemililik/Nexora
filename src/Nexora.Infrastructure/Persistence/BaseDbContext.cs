using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for all module contexts. Provides schema-per-tenant support,
/// audit fields, soft delete global filters, and domain event dispatching.
/// </summary>
public abstract class BaseDbContext(
    DbContextOptions options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null) : DbContext(options), IUnitOfWork
{
    private readonly DomainEventDispatcher? _domainEventDispatcher = domainEventDispatcher;

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
        }

    }

    /// <summary>
    /// Applies global soft delete query filters. Call this in child OnModelCreating
    /// AFTER ApplyConfigurationsFromAssembly so all entity conversions are registered.
    /// </summary>
    protected static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildSoftDeleteFilter(entityType.ClrType));
            }
        }
    }

    /// <summary>Saves changes, converts hard deletes to soft deletes, sets audit fields, and dispatches domain events.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ConvertDeletesAndSetAuditFields();
        var result = await base.SaveChangesAsync(ct);

        if (_domainEventDispatcher is not null)
            await _domainEventDispatcher.DispatchEventsAsync(this, ct);

        return result;
    }

    /// <summary>Saves changes, converts hard deletes to soft deletes, and sets audit fields.</summary>
    public override int SaveChanges()
    {
        ConvertDeletesAndSetAuditFields();
        return base.SaveChanges();
    }

    /// <summary>Saves changes, converts hard deletes to soft deletes, and sets audit fields.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertDeletesAndSetAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ConvertDeletesAndSetAuditFields()
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
            // Convert hard deletes to soft deletes for ISoftDeletable entities
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
                entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = userId;
                continue;
            }

            if (!IsAuditableEntity(entry.Entity.GetType()))
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

    /// <summary>Builds a lambda expression: e => !e.IsDeleted</summary>
    private static LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var condition = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(condition, parameter);
    }
}
