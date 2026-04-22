using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Infrastructure.Audit;

/// <summary>
/// EF Core interceptor that snapshots ChangeTracker entries on every <c>SaveChanges</c> and
/// writes them into the scoped <see cref="IAuditStateCapture"/> so the MediatR audit behavior
/// can populate the BeforeState / AfterState / Changes JSONB fields on the audit entry.
/// </summary>
/// <remarks>
/// Only <see cref="IAuditable"/> entities (i.e. <c>AuditableEntity&lt;T&gt;</c>) are captured.
/// Audit/Outbox/Inbox infrastructure tables and owned value objects are ignored to avoid
/// recording the audit trail of the audit system itself.
/// Capture failures NEVER block SaveChanges — any exception is logged and swallowed.
/// </remarks>
public sealed class AuditChangeTrackerInterceptor(
    IAuditStateCapture capture,
    ILogger<AuditChangeTrackerInterceptor> logger) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        TryCapture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        TryCapture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void TryCapture(DbContext? context)
    {
        if (context is null)
            return;

        try
        {
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (!ShouldCapture(entry))
                    continue;

                var kind = ResolveKind(entry);
                var (before, after, delta) = BuildSnapshots(entry, kind);

                capture.Capture(new CapturedEntityChange(
                    EntityType: entry.Entity.GetType().Name,
                    EntityId: ResolveEntityId(entry),
                    Kind: kind,
                    Before: before,
                    After: after,
                    Delta: delta));
            }
        }
        // [ADR] Audit capture must never block writes. See CODING_STANDARDS.md catch(Exception) rule.
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit state capture failed; proceeding with SaveChanges without diff");
        }
    }

    private static bool ShouldCapture(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            return false;

        // Only capture domain entities that inherit AuditableEntity<T>.
        var type = entry.Entity.GetType();
        while (type != null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AuditableEntity<>))
                return true;
            type = type.BaseType;
        }
        return false;
    }

    private static EntityChangeKind ResolveKind(EntityEntry entry)
    {
        // Soft-delete is modelled here: BaseDbContext converts Deleted→Modified + IsDeleted=true
        // AFTER this interceptor runs, so we still see State=Deleted when a hard-delete is requested.
        // For an already-converted soft delete we detect IsDeleted transitioning false→true below.
        if (entry.State == EntityState.Added) return EntityChangeKind.Added;
        if (entry.State == EntityState.Deleted) return EntityChangeKind.Deleted;

        if (entry.Entity is ISoftDeletable)
        {
            var prop = entry.Property(nameof(ISoftDeletable.IsDeleted));
            if (!Equals(prop.OriginalValue, prop.CurrentValue) && prop.CurrentValue is true)
                return EntityChangeKind.Deleted;
        }

        return EntityChangeKind.Modified;
    }

    private static (Dictionary<string, object?> Before, Dictionary<string, object?> After,
        Dictionary<string, object?> Delta) BuildSnapshots(EntityEntry entry, EntityChangeKind kind)
    {
        var before = new Dictionary<string, object?>(StringComparer.Ordinal);
        var after = new Dictionary<string, object?>(StringComparer.Ordinal);
        var delta = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var prop in entry.Properties)
        {
            if (IsExcludedField(prop.Metadata.Name))
                continue;

            var original = UnwrapValue(prop.OriginalValue);
            var current = UnwrapValue(prop.CurrentValue);

            switch (kind)
            {
                case EntityChangeKind.Added:
                    after[prop.Metadata.Name] = current;
                    delta[prop.Metadata.Name] = new { From = (object?)null, To = current };
                    break;
                case EntityChangeKind.Deleted:
                    before[prop.Metadata.Name] = original;
                    delta[prop.Metadata.Name] = new { From = original, To = (object?)null };
                    break;
                default: // Modified
                    before[prop.Metadata.Name] = original;
                    after[prop.Metadata.Name] = current;
                    if (!Equals(original, current))
                        delta[prop.Metadata.Name] = new { From = original, To = current };
                    break;
            }
        }

        return (before, after, delta);
    }

    private static string? ResolveEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
            return null;

        var values = key.Properties
            .Select(p => UnwrapValue(entry.Property(p.Name).CurrentValue)?.ToString())
            .Where(v => v is not null)
            .ToArray();

        return values.Length == 0 ? null : string.Join(":", values);
    }

    /// <summary>
    /// Unwraps a single-value wrapper (<c>readonly record struct Xxx(Guid Value)</c> strongly-typed IDs,
    /// or any other type exposing a single scalar property) so that JSON output is flat and human-readable.
    /// Primitives, strings, DateTime/Offset, and enums pass through unchanged.
    /// </summary>
    private static object? UnwrapValue(object? value)
    {
        if (value is null) return null;

        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
            || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan) || type.IsEnum)
            return value;

        // Single-property records/structs that look like strongly-typed IDs (e.g. TenantId(Guid Value)).
        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (props.Length == 1 && props[0].CanRead)
            return UnwrapValue(props[0].GetValue(value));

        return value;
    }

    /// <summary>
    /// Fields excluded from audit snapshots: auto-populated audit stamps, tenant/org scoping columns,
    /// soft-delete bookkeeping, and concurrency tokens. Leaking these to UI adds noise without informing
    /// the reviewer, and exposing TenantId cross-references is a multi-tenancy leak risk.
    /// </summary>
    private static bool IsExcludedField(string name) => name is
        "CreatedAt" or "CreatedBy" or "UpdatedAt" or "UpdatedBy"
        or "DeletedAt" or "DeletedBy" or "IsDeleted"
        or "TenantId" or "OrganizationId" or "RowVersion";
}
