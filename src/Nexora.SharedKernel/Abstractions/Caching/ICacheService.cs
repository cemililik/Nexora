namespace Nexora.SharedKernel.Abstractions.Caching;

/// <summary>
/// Two-level cache abstraction (L1 in-memory + L2 distributed via Dapr).
/// All modules MUST use this instead of IDistributedCache or DaprClient.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheOptions? options = null,
        CancellationToken ct = default);

    Task SetAsync<T>(
        string key,
        T value,
        CacheOptions? options = null,
        CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

public sealed record CacheOptions
{
    public TimeSpan L1Ttl { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan L2Ttl { get; init; } = TimeSpan.FromMinutes(15);
    public string[] Tags { get; init; } = [];
}
