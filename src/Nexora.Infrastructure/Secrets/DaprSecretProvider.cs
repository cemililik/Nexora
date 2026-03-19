using System.Text.Json;
using Dapr.Client;
using Nexora.SharedKernel.Abstractions.Secrets;

namespace Nexora.Infrastructure.Secrets;

/// <summary>
/// Secret provider backed by Dapr Secret Store (Vault in prod, local file/K8s Secrets in dev).
/// </summary>
public sealed class DaprSecretProvider(DaprClient daprClient) : ISecretProvider
{
    private const string SecretStoreName = "secretstore";

    public async Task<string> GetSecretAsync(string key, CancellationToken ct = default)
    {
        var secret = await daprClient.GetSecretAsync(SecretStoreName, key, cancellationToken: ct);
        return secret.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Secret '{key}' not found in store.");
    }

    public async Task<T> GetSecretAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var json = await GetSecretAsync(key, ct);
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize secret '{key}' to {typeof(T).Name}.");
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct = default)
    {
        var secrets = await daprClient.GetBulkSecretAsync(SecretStoreName, cancellationToken: ct);

        return secrets
            .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
