using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.Gdpr;

namespace Nexora.Infrastructure.Gdpr;

/// <summary>
/// Returns <see cref="GdprRenamedTableScanResult.Empty"/> without invoking
/// the redaction callback. Used by unit tests that build the GDPR
/// integration handlers against EF InMemory and don't need to exercise
/// the renamed-table scan path.
/// </summary>
public sealed class NoOpGdprRenamedTableScanner<TDbContext> : IGdprRenamedTableScanner<TDbContext>
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public Task<GdprRenamedTableScanResult> ScanAsync(
        string moduleName,
        Func<RenamedTableInfo, CancellationToken, Task<int>> redactSingleTableAsync,
        CancellationToken ct)
        => Task.FromResult(GdprRenamedTableScanResult.Empty);
}
