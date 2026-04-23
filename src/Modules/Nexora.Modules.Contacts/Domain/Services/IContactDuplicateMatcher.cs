namespace Nexora.Modules.Contacts.Domain.Services;

/// <summary>
/// Looks up existing contacts by normalized email or phone to prevent duplicates
/// during bulk imports and other automated ingestion paths.
/// </summary>
public interface IContactDuplicateMatcher
{
    /// <summary>
    /// Returns the id of the first existing contact in the given (tenant, organization)
    /// scope that matches the supplied email (normalized to lowercase + trimmed) or phone
    /// (E.164 normalized). Returns <c>null</c> when no match is found.
    /// </summary>
    /// <param name="tenantId">Tenant scope.</param>
    /// <param name="organizationId">Organization scope within the tenant.</param>
    /// <param name="email">Optional email to match.</param>
    /// <param name="phone">Optional phone to match.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Guid?> FindExistingContactIdAsync(
        Guid tenantId,
        Guid organizationId,
        string? email,
        string? phone,
        CancellationToken ct);
}
