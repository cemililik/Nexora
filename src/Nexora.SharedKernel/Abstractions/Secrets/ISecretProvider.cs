namespace Nexora.SharedKernel.Abstractions.Secrets;

/// <summary>
/// Secret provider backed by Dapr Secret Store (Vault in prod, K8s Secrets in dev).
/// All modules MUST use this instead of direct Vault/env var access.
/// </summary>
public interface ISecretProvider
{
    Task<string> GetSecretAsync(string key, CancellationToken ct = default);

    Task<T> GetSecretAsync<T>(string key, CancellationToken ct = default) where T : class;

    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct = default);
}
