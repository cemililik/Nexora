using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Always-acquires <see cref="IUninstallAdvisoryLock"/> for unit tests and
/// non-relational providers (EF InMemory). Production DI registers the
/// Postgres implementation; this is exported to keep test wiring trivial.
/// </summary>
public sealed class NoOpUninstallAdvisoryLock : IUninstallAdvisoryLock
{
    /// <inheritdoc />
    public Task<IAsyncDisposable?> AcquireAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IAsyncDisposable?>(new Handle());

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
