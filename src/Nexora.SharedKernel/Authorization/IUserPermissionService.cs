namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// Loads the effective permission set for a user within a tenant.
/// Implementations are expected to cache results for performance.
/// </summary>
public interface IUserPermissionService
{
    /// <summary>
    /// Returns the distinct set of permission strings (e.g., <c>identity.users.read</c>)
    /// granted to the user identified by <paramref name="keycloakUserId"/> within
    /// the tenant identified by <paramref name="tenantId"/>.
    /// </summary>
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        string tenantId,
        string keycloakUserId,
        CancellationToken ct = default);
}
