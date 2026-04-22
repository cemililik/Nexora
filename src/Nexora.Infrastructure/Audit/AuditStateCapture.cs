using Nexora.SharedKernel.Abstractions.Audit;

namespace Nexora.Infrastructure.Audit;

/// <summary>
/// Default scoped buffer for entity change snapshots. One instance per request/scope;
/// the <see cref="AuditChangeTrackerInterceptor"/> writes into it and
/// <c>AuditLogBehavior</c> reads from it after the handler completes.
/// </summary>
public sealed class AuditStateCapture : IAuditStateCapture
{
    private readonly List<CapturedEntityChange> _changes = [];

    /// <inheritdoc />
    public IReadOnlyList<CapturedEntityChange> Changes => _changes;

    /// <inheritdoc />
    public void Capture(CapturedEntityChange change) => _changes.Add(change);

    /// <inheritdoc />
    public void Clear() => _changes.Clear();
}
