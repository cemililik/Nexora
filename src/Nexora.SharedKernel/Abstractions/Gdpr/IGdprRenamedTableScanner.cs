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
    /// Module-supplied callback. Receives a <see cref="RenamedTableInfo"/>
    /// carrying both the bare table name (for prefix dispatching) and the
    /// schema-qualified identifier (for parameterised SQL — handlers MUST
    /// use <see cref="RenamedTableInfo.QualifiedIdentifier"/> rather than
    /// concatenating the bare name into a SQL string, which would depend
    /// on the connection's <c>search_path</c>). The
    /// <see cref="RenamedTableInfo.AllDiscoveredBareNames"/> set lets the
    /// handler verify a sibling renamed table exists before joining
    /// against it (review #56 round-2 — UPDATE … IN (SELECT … FROM
    /// missing_table) would otherwise raise Postgres 42P01).
    /// Returns the row count the redaction UPDATE affected.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// <para>
    /// <b>Callback exception contract.</b>
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="OperationCanceledException"/> / <see cref="TaskCanceledException"/>
    ///     (or any exception thrown when <paramref name="ct"/> is cancelled)
    ///     MUST immediately propagate and abort the scan.
    ///   </description></item>
    ///   <item><description>
    ///     All other exceptions thrown by <paramref name="redactSingleTableAsync"/>
    ///     MUST be handled by implementations: either caught and logged so
    ///     remaining tables continue, or re-thrown to abort the scan.
    ///     Implementations MUST document which strategy they use.
    ///   <c>PostgresGdprRenamedTableScanner&lt;TDbContext&gt;</c> catches and
    ///     continues; <c>NoOpGdprRenamedTableScanner</c> never invokes the callback.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <returns>
    /// Aggregate of tables discovered and rows redacted across all renamed
    /// tables in the module's prefix namespace.
    /// <see cref="GdprRenamedTableScanResult.Empty"/> when none were found or
    /// the provider is non-relational (tests).
    /// </returns>
    Task<GdprRenamedTableScanResult> ScanAsync(
        string moduleName,
        Func<RenamedTableInfo, CancellationToken, Task<int>> redactSingleTableAsync,
        CancellationToken ct);
}

/// <summary>
/// Per-renamed-table context handed to the redaction callback. Carries
/// the bare table name (for the handler's prefix-based dispatch), the
/// schema-qualified identifier (for SQL), and the full discovered set
/// (for sibling-existence checks).
/// </summary>
public sealed record RenamedTableInfo(
    string BareName,
    string QualifiedIdentifier,
    IReadOnlySet<string> AllDiscoveredBareNames);

/// <summary>
/// Generic marker so each module can register its own scanner against its
/// own <c>DbContext</c>. The <c>TDbContext</c> type parameter is purely for
/// DI disambiguation; it carries no runtime contract beyond the base
/// <see cref="IGdprRenamedTableScanner"/>.
/// </summary>
/// <remarks>
/// <para>
/// <typeparamref name="TDbContext"/> exists only to disambiguate DI
/// registrations — analogous to <c>IOptions&lt;T&gt;</c> where the type
/// parameter selects the registration without adding behaviour. It has no
/// runtime contract and is never used inside an implementation. The
/// <c>CA1715</c> / <c>S2326</c> pragmas below suppress warnings about the
/// unused type parameter for precisely this reason; future maintainers should
/// keep the suppressions and this remark rather than renaming or removing the
/// parameter.
/// </para>
/// </remarks>
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
