using Nexora.SharedKernel.Abstractions.Configuration;

namespace Nexora.SharedKernel.Abstractions.FeatureFlags;

/// <summary>
/// T-016: progressive-rollout flag evaluation. Reads the effective value
/// for a feature flag honouring (platform default → tenant override) and,
/// optionally, a user-id dimension for percentage-rollout backends.
/// </summary>
/// <remarks>
/// <para>
/// <b>Default backend.</b> The platform's first implementation is
/// <c>TenantConfigFeatureFlagService</c>, which reads from
/// <see cref="ITenantConfiguration"/> under the
/// <c>feature_flags.{key}</c> namespace. Operators toggle a flag for one
/// tenant by writing the value through the admin endpoint; the next read
/// observes it (cached at the same TTL as tenant config).
/// </para>
/// <para>
/// <b>LaunchDarkly / future backend.</b> The interface keeps a
/// <paramref name="userId"/> parameter so a percentage-rollout backend
/// can hash on it without changing call sites. Default backend ignores
/// <paramref name="userId"/> — flag values in tenant config are
/// boolean for simplicity at this stage.
/// </para>
/// <para>
/// <b>Naming convention.</b> Flag keys are dotted-snake strings —
/// <c>module.scope.feature</c> — to match the rest of the
/// <see cref="IConfigurationResolver"/> key namespace.
/// Examples: <c>crm.pipeline.kanban_v2</c>, <c>contacts.import.parallel_v2</c>.
/// </para>
/// </remarks>
public interface IFeatureFlagService
{
    /// <summary>
    /// Returns the effective value for <paramref name="flagKey"/> in the
    /// current tenant context. <see langword="false"/> when no value is
    /// set — flag absence means "off" everywhere by convention.
    /// </summary>
    /// <param name="flagKey">Dotted-snake flag identifier (e.g. <c>crm.pipeline.kanban_v2</c>).</param>
    /// <param name="userId">
    /// Optional user identifier — passed through to backends that
    /// support per-user percentage rollouts. Default backend ignores it.
    /// </param>
    Task<bool> IsEnabledAsync(string flagKey, Guid? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Sets a flag value for the current tenant. Only callable by users
    /// holding the <c>identity.feature_flags.manage</c> permission — the
    /// endpoint enforces this; the service itself does not re-check.
    /// </summary>
    Task SetAsync(string flagKey, bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Returns every flag-keyed entry in the current tenant's config so
    /// the admin UI can render a list. Returns an empty dictionary when
    /// nothing is set.
    /// </summary>
    Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken ct = default);
}
