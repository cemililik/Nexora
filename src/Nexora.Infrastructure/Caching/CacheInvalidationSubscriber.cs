using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Nexora.Infrastructure.Caching;

/// <summary>
/// Handles cache invalidation events received from other instances via Dapr pub/sub.
/// When another instance calls <see cref="DaprCacheService.RemoveByPrefixAsync"/>,
/// it publishes a <see cref="CacheInvalidationEvent"/>. This handler clears matching keys
/// from the local L1 memory cache and the tracked keys dictionary, ensuring
/// cross-instance cache consistency in multi-instance deployments.
/// </summary>
public sealed class CacheInvalidationHandler(
    IMemoryCache memoryCache,
    ILogger<CacheInvalidationHandler> logger)
{
    internal const string TopicName = "nexora.cache.invalidation";

    /// <summary>
    /// Processes a cache invalidation event received from Dapr pub/sub.
    /// Clears matching keys from the local L1 cache and tracked keys.
    /// </summary>
    public void Handle(CacheInvalidationEvent invalidationEvent)
    {
        if (invalidationEvent.InstanceId == DaprCacheService.InstanceId)
        {
            // Skip events published by this instance — already handled locally
            return;
        }

        var prefix = invalidationEvent.Prefix;
        var keysToRemove = DaprCacheService.TrackedKeys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        if (keysToRemove.Count == 0)
            return;

        logger.LogDebug(
            "Cache invalidation received from instance {InstanceId} — clearing {Count} local keys with prefix '{Prefix}'",
            invalidationEvent.InstanceId, keysToRemove.Count, prefix);

        foreach (var key in keysToRemove)
        {
            memoryCache.Remove(key);
            DaprCacheService.TrackedKeys.TryRemove(key, out _);
        }
    }
}
