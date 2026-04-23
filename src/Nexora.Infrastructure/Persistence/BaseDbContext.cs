using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

    /// <summary>
    /// When <c>true</c>, the soft-delete interceptor in <see cref="ConvertDeletesAndSetAuditFields"/>
    /// is bypassed and <see cref="EntityState.Deleted"/> entries are permanently removed.
    /// Intended ONLY for GDPR Article 17 hard-delete workflows and uninstall cleanups.
    /// Toggle via <see cref="EnterHardDeleteScope"/>; direct mutation is disallowed.
    /// </summary>
    public bool IsHardDeleteModeEnabled { get; private set; }

    /// <summary>
    /// Enables hard-delete mode for the lifetime of the returned scope. On <see cref="IDisposable.Dispose"/>
    /// the flag is restored to <c>false</c>, guaranteeing we never leak the bypass beyond the caller.
    /// Scope-based helper; not thread-safe. Intended for use within a single-scoped DbContext
    /// (Hangfire job scope, request scope).
    /// </summary>
    /// <returns>A disposable handle that resets the flag on <see cref="IDisposable.Dispose"/>.</returns>
    public IDisposable EnterHardDeleteScope() => new HardDeleteScope(this);

    /// <summary>
    /// Stores the previous value of <see cref="IsHardDeleteModeEnabled"/> and restores it on
    /// dispose so nested scopes compose correctly (inner scope must not flip the flag off
    /// while an outer scope is still active). Dispose is idempotent.
    /// </summary>
    private sealed class HardDeleteScope : IDisposable
    {
        private readonly BaseDbContext _context;
        private readonly bool _previousValue;
        private bool _disposed;

        public HardDeleteScope(BaseDbContext context)
        {
            _context = context;
            _previousValue = context.IsHardDeleteModeEnabled;
            context.IsHardDeleteModeEnabled = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context.IsHardDeleteModeEnabled = _previousValue;
            _disposed = true;
        }
    }

    /// <summary>
    /// Returns the current tenant schema name for model cache keying.
    /// Returns "default" if no tenant context is set (e.g., in tests or platform operations).
    /// </summary>
    internal string GetCurrentSchema()
    {
        try
        {
            return TenantContextAccessor.Current.SchemaName;
        }
        catch (InvalidOperationException)
        {
            return "default";
        }
    }

    /// <summary>
    /// Replaces the default model cache key factory with <see cref="TenantModelCacheKeyFactory"/>
    /// so each tenant schema gets its own cached model. Without this, the first tenant's schema
    /// is cached and reused for all subsequent tenants.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
    }

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

    /// <summary>Saves changes, converts hard deletes to soft deletes, sets audit fields, and dispatches domain events.</summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        ConvertDeletesAndSetAuditFields();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        if (_domainEventDispatcher is not null)
            await _domainEventDispatcher.DispatchEventsAsync(this, ct);

        return result;
    }

    /// <summary>Saves changes, converts hard deletes to soft deletes, sets audit fields, and dispatches domain events.</summary>
    public override int SaveChanges()
    {
        ConvertDeletesAndSetAuditFields();
        var result = base.SaveChanges();

        if (_domainEventDispatcher is not null)
            _domainEventDispatcher.DispatchEvents(this);

        return result;
    }

    /// <summary>Saves changes, converts hard deletes to soft deletes, sets audit fields, and dispatches domain events.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertDeletesAndSetAuditFields();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);

        if (_domainEventDispatcher is not null)
            _domainEventDispatcher.DispatchEvents(this);

        return result;
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
            // Convert hard deletes to soft deletes for ISoftDeletable entities,
            // unless hard-delete mode is explicitly enabled (GDPR Article 17).
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable && !IsHardDeleteModeEnabled)
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
