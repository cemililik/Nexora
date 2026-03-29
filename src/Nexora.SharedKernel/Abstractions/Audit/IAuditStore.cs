namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Persists audit log entries to the underlying storage.
/// </summary>
public interface IAuditStore
{
    /// <summary>Saves an audit entry to the store.</summary>
    /// <param name="entry">The audit entry to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(AuditEntry entry, CancellationToken ct);
}
