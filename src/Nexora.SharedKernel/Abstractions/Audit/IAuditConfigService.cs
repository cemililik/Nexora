namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Determines whether audit logging is enabled for a given module and operation.
/// Implementations may read from configuration, database, or feature flags.
/// </summary>
public interface IAuditConfigService
{
    /// <summary>Checks if auditing is enabled for the specified module and operation.</summary>
    /// <param name="module">The module name (e.g., "Contacts").</param>
    /// <param name="operation">The operation name (e.g., "CreateContact").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="defaultEnabled">The default value when no explicit setting exists. Defaults to true.</param>
    /// <returns>True if auditing is enabled; otherwise false.</returns>
    Task<bool> IsEnabledAsync(string module, string operation, CancellationToken ct, bool defaultEnabled = true);
}
