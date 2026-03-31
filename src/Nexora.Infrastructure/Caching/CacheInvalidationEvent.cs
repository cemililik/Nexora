namespace Nexora.Infrastructure.Caching;

/// <summary>
/// Pub/sub event published when cache keys are invalidated by prefix.
/// Other instances subscribe to this event to clear their local L1 cache and tracked keys,
/// ensuring cross-instance cache consistency in multi-instance deployments.
/// </summary>
/// <param name="Prefix">The full (tenant-prefixed) cache key prefix that was invalidated.</param>
/// <param name="InstanceId">
/// The unique identifier of the instance that originated the invalidation,
/// so it can skip processing its own events.
/// </param>
public sealed record CacheInvalidationEvent(string Prefix, string InstanceId);
