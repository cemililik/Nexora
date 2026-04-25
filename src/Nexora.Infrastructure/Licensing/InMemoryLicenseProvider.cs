using Nexora.SharedKernel.Abstractions.Licensing;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-014: hot-swappable read-side of the license snapshot. Backed by a
/// single field that <see cref="LicenseReloadService"/> updates via
/// <see cref="System.Threading.Interlocked.Exchange{T}"/>; readers use
/// <see cref="System.Threading.Volatile.Read"/> so a mid-request reload
/// never surfaces a torn intermediate.
/// </summary>
public sealed class InMemoryLicenseProvider : ILicenseProvider
{
    private LicenseSnapshot? _current;

    /// <inheritdoc />
    public LicenseSnapshot? Current => Volatile.Read(ref _current);

    /// <summary>
    /// Atomically swaps the current snapshot. Called by <c>LicenseReloadService</c>
    /// after successful validation; returns the previously-active snapshot
    /// (or null on first set) so callers can log the transition.
    /// </summary>
    internal LicenseSnapshot? Set(LicenseSnapshot newSnapshot) =>
        Interlocked.Exchange(ref _current, newSnapshot);
}
