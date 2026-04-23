namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Scoped, request-bound buffer that collects entity-level change snapshots captured by the
/// EF Core SaveChanges interceptor. The <c>AuditLogBehavior</c> reads accumulated changes after
/// the handler returns and serializes them into the audit entry's BeforeState/AfterState/Changes.
/// </summary>
public interface IAuditStateCapture
{
    /// <summary>Records a single entity-level change observed during <c>SaveChangesAsync</c>.</summary>
    void Capture(CapturedEntityChange change);

    /// <summary>All changes captured in the current scope, in the order they were observed.</summary>
    IReadOnlyList<CapturedEntityChange> Changes { get; }

    /// <summary>Clears buffered changes. Called by the behavior after serialization.</summary>
    void Clear();
}

/// <summary>The kind of change observed for a single entity during <c>SaveChangesAsync</c>.</summary>
public enum EntityChangeKind
{
    /// <summary>Entity was newly added.</summary>
    Added,
    /// <summary>Entity had one or more properties modified.</summary>
    Modified,
    /// <summary>Entity was hard-deleted, or soft-deleted via <c>IsDeleted=true</c>.</summary>
    Deleted
}

/// <summary>
/// A single entity change snapshot — property values before and after,
/// plus a delta (only properties whose value actually changed).
/// </summary>
public sealed record CapturedEntityChange(
    string EntityType,
    string? EntityId,
    EntityChangeKind Kind,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After,
    IReadOnlyDictionary<string, object?> Delta);
