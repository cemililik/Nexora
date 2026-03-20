namespace Nexora.SharedKernel.Abstractions.Secrets;

/// <summary>
/// Secret provider backed by Dapr Secret Store (Vault in prod, K8s Secrets in dev).
/// All modules MUST use this instead of direct Vault/env var access.
/// </summary>
public interface ISecretProvider
{
    /// <summary>Gets a secret value by key.</summary>
    Task<string> GetSecretAsync(string key, CancellationToken ct = default);

    /// <summary>Gets a secret by key and deserializes it to the specified type.</summary>
    Task<T> GetSecretAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Gets all secrets whose keys start with the given prefix.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct = default);
}
