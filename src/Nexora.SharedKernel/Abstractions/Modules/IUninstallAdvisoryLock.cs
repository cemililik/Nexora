namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Tenant-scoped session advisory lock that spans every per-module
/// transaction inside a cascade-uninstall sequence. Per ADR-0031 / T-026
/// the lock MUST survive across module commits — a transaction-scoped
/// lock would release between modules and let a concurrent operator
/// interleave a second cascade.
/// </summary>
/// <remarks>
/// <para>
/// Two implementations ship: the Postgres impl (production) opens a
/// dedicated <c>NpgsqlConnection</c> and calls <c>pg_try_advisory_lock</c>
/// keyed on <c>hashtext('uninstall:' || tenantId)</c>; the no-op impl
/// (tests / non-relational providers) returns an immediately-acquired
/// handle so unit tests don't need a Postgres instance.
/// </para>
/// </remarks>
public interface IUninstallAdvisoryLock
{
    /// <summary>
    /// Attempts to acquire the cascade lock for <paramref name="tenantId"/>.
    /// Returns <c>null</c> when another session already holds the lock —
    /// the caller MUST surface this as a "cascade in progress" error
    /// rather than proceed.
    /// </summary>
    Task<IAsyncDisposable?> AcquireAsync(Guid tenantId, CancellationToken ct);
}
