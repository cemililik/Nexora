namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Three-tier configuration resolver: platform cap → tenant default → organization
/// override. Precedence is <c>cap.forced &gt; org &gt; tenant &gt; cap.default</c>.
/// Replaces direct reads of <see cref="ITenantConfiguration"/> for keys that are
/// organization-scoped or subject to platform policy caps.
/// </summary>
/// <remarks>
/// See ADR-0025 (Org-Scoped Compliance Config with Platform-Level Caps).
/// <para>
/// The tenant + organization context is resolved implicitly from the request-scoped
/// <c>ITenantContextAccessor</c> — callers do not pass IDs explicitly.
/// </para>
/// </remarks>
public interface IConfigurationResolver
{
    /// <summary>
    /// Reads the effective configuration value for <paramref name="key"/>, honouring
    /// the three-tier precedence.
    /// </summary>
    /// <returns>
    /// The effective value deserialized to <typeparamref name="T"/>, or the
    /// <typeparamref name="T"/> default when no layer supplies a value.
    /// </returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Writes an organization-scope override for <paramref name="key"/> in the caller's
    /// current org context. Records a policy-audit row atomically with the update.
    /// When the platform cap does not allow the requested value, the call fails.
    /// </summary>
    /// <param name="reason">
    /// Required free-text justification recorded in the policy audit trail for later
    /// compliance review. Max 500 characters.
    /// </param>
    /// <exception cref="ComplianceCapViolationException">
    /// Thrown when the platform cap rejects the override — either <c>cap.Allowed=false</c>
    /// (no override permitted) or <c>cap.Forced=true</c> with a contradicting value.
    /// Callers at the HTTP boundary (e.g. <c>ComplianceConfigEndpoints</c>) should convert
    /// this to a localized 409/Conflict using <c>LocalizationKey</c>, <c>IsForced</c>, and
    /// <c>ForcedValue</c> from the exception. The rejection is itself recorded in the
    /// policy-audit table before the exception is raised.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the caller lacks an organization in the current tenant context
    /// (org overrides are inherently scoped to an org) or when there is no
    /// authenticated user id to attribute the change to in the audit row.
    /// </exception>
    Task SetOrgOverrideAsync<T>(string key, T value, string reason, CancellationToken ct = default);

    /// <summary>
    /// Removes the organization-scope override for <paramref name="key"/>, letting the
    /// tenant default (or cap) take effect. Records a policy-audit row.
    /// </summary>
    /// <param name="reason">
    /// Required free-text justification, same semantics as <see cref="SetOrgOverrideAsync{T}"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no organization in the current tenant context — clearing an
    /// override requires a specific org scope.
    /// </exception>
    Task ClearOrgOverrideAsync(string key, string reason, CancellationToken ct = default);

    /// <summary>
    /// Returns the effective value along with the active cap and each layer's contribution.
    /// Used by the admin UI to display "tenant default / org override / effective / cap
    /// badge" breakdown and by policy audit views.
    /// </summary>
    Task<ResolvedConfiguration<T>> GetResolvedAsync<T>(string key, CancellationToken ct = default);
}

/// <summary>
/// Diagnostic view of a configuration resolution — each layer's contribution plus the
/// winning value. Returned from <see cref="IConfigurationResolver.GetResolvedAsync{T}"/>.
/// </summary>
public sealed record ResolvedConfiguration<T>(
    T? Effective,
    T? TenantDefault,
    T? OrgOverride,
    ComplianceCap Cap,
    ResolutionLayer WinningLayer);

/// <summary>Identifies which of the three layers supplied the effective value.</summary>
public enum ResolutionLayer
{
    /// <summary>No layer supplied a value; <c>default(T)</c> returned.</summary>
    None,
    /// <summary>The platform cap's own value won (typically because <c>cap.forced</c>).</summary>
    Cap,
    /// <summary>The tenant default won — no org override existed (or cap collapsed it).</summary>
    TenantDefault,
    /// <summary>The organization override won.</summary>
    OrgOverride
}
