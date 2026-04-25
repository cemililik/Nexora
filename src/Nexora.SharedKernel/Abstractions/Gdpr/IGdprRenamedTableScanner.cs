namespace Nexora.SharedKernel.Abstractions.Gdpr;

/// <summary>
/// T-027 escape hatch: discovers tables in the current tenant's schema whose
/// names match the canonical <c>{module}_{entity}_del_{timestamp}</c> shape
/// (ADR-0028) and dispatches a per-table redaction callback supplied by the
/// caller. Lets each module's
/// <c>ContactGdprDeletedIntegrationEventHandler</c> close the renamed-table
/// gap in GDPR Article 17 erasure without each handler reinventing the
/// table-discovery query.
/// </summary>
/// <remarks>
/// <para>
/// The discovery query MUST be regex-based (NOT SQL <c>LIKE '_%'</c>, which
/// treats <c>_</c> as a single-character wildcard and would cross-bleed
/// between modules whose names share a prefix). The accepted regex is
/// <c>^{module}_[a-z0-9_]+_del_(?:[0-9]{14}|[0-9]{8}_[0-9]{6})$</c> —
/// anchored, requires the literal <c>_del_</c> marker, and accepts both
/// timestamp shapes T-026 may emit.
/// </para>
/// <para>
/// The redaction callback receives one renamed table at a time so the
/// caller can apply module-specific column updates with the same SQL
/// shape it already uses for the canonical table — only the table name
/// swaps in. The scanner sums affected row counts across tables; the
/// callback is responsible for parameterising <c>ContactId</c> and
/// <c>TenantId</c> against the renamed table.
/// </para>
/// </remarks>
public interface IGdprRenamedTableScanner
{
    /// <summary>
    /// Discovers renamed tables for <paramref name="moduleName"/> in the
    /// current tenant's schema and invokes
    /// <paramref name="redactSingleTableAsync"/> once per discovered table.
    /// Returns the aggregate of (tables discovered, rows redacted).
    /// </summary>
    /// <param name="moduleName">
    /// Snake-case module slug (e.g. <c>contacts</c>, <c>documents</c>).
    /// Must match the prefix the uninstall path used.
    /// </param>
    /// <param name="redactSingleTableAsync">
    /// Module-supplied callback. Receives the renamed table name (already
    /// validated against the canonical shape) and returns the row count
    /// the redaction UPDATE affected.
    /// </param>
    Task<GdprRenamedTableScanResult> ScanAsync(
        string moduleName,
        Func<string, CancellationToken, Task<int>> redactSingleTableAsync,
        CancellationToken ct);
}

/// <summary>
/// Generic marker so each module can register its own scanner against its
/// own <c>DbContext</c>. The <c>TDbContext</c> type parameter is purely for
/// DI disambiguation; it carries no runtime contract beyond the base
/// <see cref="IGdprRenamedTableScanner"/>.
/// </summary>
#pragma warning disable CA1715, S2326 // generic type param exists for DI shape, not behaviour
public interface IGdprRenamedTableScanner<TDbContext> : IGdprRenamedTableScanner { }
#pragma warning restore CA1715, S2326

/// <summary>
/// Aggregate result of a renamed-table scan.
/// </summary>
public sealed record GdprRenamedTableScanResult(int TablesScanned, int RowsRedacted)
{
    /// <summary>Empty result returned when the scanner is a no-op (tests, non-relational provider).</summary>
    public static readonly GdprRenamedTableScanResult Empty = new(0, 0);
}
