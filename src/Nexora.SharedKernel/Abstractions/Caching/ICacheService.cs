namespace Nexora.SharedKernel.Abstractions.Caching;

/// <summary>
/// Two-level cache abstraction (L1 in-memory + L2 distributed via Dapr).
/// All modules MUST use this instead of IDistributedCache or DaprClient.
/// </summary>
public interface ICacheService
{
    /// <summary>Gets a cached value by key, or null if not found.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Gets a cached value by key, or creates and caches it using the factory.</summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheOptions? options = null,
        CancellationToken ct = default);

    /// <summary>Sets a value in both L1 and L2 caches.</summary>
    Task SetAsync<T>(
        string key,
        T value,
        CacheOptions? options = null,
        CancellationToken ct = default);

    /// <summary>Removes a cached value by key from both cache levels.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes all cached values whose keys start with the given prefix.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

/// <summary>
/// Configuration options for cache entry TTL at each level.
/// </summary>
public sealed record CacheOptions
{
    public TimeSpan L1Ttl { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan L2Ttl { get; init; } = TimeSpan.FromMinutes(15);
    public string[] Tags { get; init; } = [];
}
