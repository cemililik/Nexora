using System.Collections.Concurrent;
using Dapr.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Caching;

/// <summary>
/// Two-level cache: L1 (in-memory) + L2 (Redis via Dapr State Store).
/// Tracks keys to support prefix-based invalidation.
/// Keys are automatically prefixed with the current tenant ID to ensure tenant isolation.
/// </summary>
public sealed class DaprCacheService(
    DaprClient daprClient,
    IMemoryCache memoryCache,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DaprCacheService> logger) : ICacheService
{
    private const string StateStoreName = "statestore";
    private static readonly ConcurrentDictionary<string, byte> _trackedKeys = new();

    /// <summary>
    /// Prefixes the cache key with the current tenant ID to ensure tenant isolation.
    /// If no tenant context is available (platform-level operations), the key is returned as-is.
    /// </summary>
    private string PrefixKey(string key)
    {
        try
        {
            var tenantId = tenantContextAccessor.Current.TenantId;
            if (!string.IsNullOrEmpty(tenantId))
                return $"{tenantId}:{key}";
        }
        catch (InvalidOperationException)
        {
            // No tenant context set — platform-level operation, no prefix needed
        }

        return key;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var prefixedKey = PrefixKey(key);

        // L1: in-memory
        if (memoryCache.TryGetValue(prefixedKey, out T? cached))
            return cached;

        // L2: Dapr state store (Redis)
        var state = await daprClient.GetStateAsync<T>(StateStoreName, prefixedKey, cancellationToken: ct);
        if (state is not null)
        {
            memoryCache.Set(prefixedKey, state, TimeSpan.FromMinutes(2));
            _trackedKeys.TryAdd(prefixedKey, 0);
        }

        return state;
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheOptions? options = null,
        CancellationToken ct = default)
    {
        var existing = await GetAsync<T>(key, ct);
        if (existing is not null)
            return existing;

        var value = await factory(ct);
        await SetAsync(key, value, options, ct);
        return value;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        CacheOptions? options = null,
        CancellationToken ct = default)
    {
        var prefixedKey = PrefixKey(key);
        options ??= new CacheOptions();

        // L1
        memoryCache.Set(prefixedKey, value, options.L1Ttl);

        // Track key for prefix invalidation
        _trackedKeys.TryAdd(prefixedKey, 0);

        // L2
        var metadata = new Dictionary<string, string>
        {
            ["ttlInSeconds"] = ((int)options.L2Ttl.TotalSeconds).ToString()
        };

        await daprClient.SaveStateAsync(StateStoreName, prefixedKey, value, metadata: metadata, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = PrefixKey(key);
        memoryCache.Remove(prefixedKey);
        _trackedKeys.TryRemove(prefixedKey, out _);
        await daprClient.DeleteStateAsync(StateStoreName, prefixedKey, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var prefixedPrefix = PrefixKey(prefix);

        var keysToRemove = _trackedKeys.Keys
            .Where(k => k.StartsWith(prefixedPrefix, StringComparison.Ordinal))
            .ToList();

        if (keysToRemove.Count == 0)
            return;

        logger.LogInformation("Removing {Count} cached keys with prefix '{Prefix}'", keysToRemove.Count, prefixedPrefix);

        foreach (var key in keysToRemove)
        {
            memoryCache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
            await daprClient.DeleteStateAsync(StateStoreName, key, cancellationToken: ct);
        }
    }
}
