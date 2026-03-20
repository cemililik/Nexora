using System.Collections.Concurrent;
using Dapr.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Caching;

namespace Nexora.Infrastructure.Caching;

/// <summary>
/// Two-level cache: L1 (in-memory) + L2 (Redis via Dapr State Store).
/// Tracks keys to support prefix-based invalidation.
/// </summary>
public sealed class DaprCacheService(
    DaprClient daprClient,
    IMemoryCache memoryCache,
    ILogger<DaprCacheService> logger) : ICacheService
{
    private const string StateStoreName = "statestore";
    private static readonly ConcurrentDictionary<string, byte> _trackedKeys = new();

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        // L1: in-memory
        if (memoryCache.TryGetValue(key, out T? cached))
            return cached;

        // L2: Dapr state store (Redis)
        var state = await daprClient.GetStateAsync<T>(StateStoreName, key, cancellationToken: ct);
        if (state is not null)
        {
            memoryCache.Set(key, state, TimeSpan.FromMinutes(2));
            _trackedKeys.TryAdd(key, 0);
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
        options ??= new CacheOptions();

        // L1
        memoryCache.Set(key, value, options.L1Ttl);

        // Track key for prefix invalidation
        _trackedKeys.TryAdd(key, 0);

        // L2
        var metadata = new Dictionary<string, string>
        {
            ["ttlInSeconds"] = ((int)options.L2Ttl.TotalSeconds).ToString()
        };

        await daprClient.SaveStateAsync(StateStoreName, key, value, metadata: metadata, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        memoryCache.Remove(key);
        _trackedKeys.TryRemove(key, out _);
        await daprClient.DeleteStateAsync(StateStoreName, key, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var keysToRemove = _trackedKeys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        if (keysToRemove.Count == 0)
            return;

        logger.LogInformation("Removing {Count} cached keys with prefix '{Prefix}'", keysToRemove.Count, prefix);

        foreach (var key in keysToRemove)
        {
            memoryCache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
            await daprClient.DeleteStateAsync(StateStoreName, key, cancellationToken: ct);
        }
    }
}
