namespace Nexora.Modules.Contacts.Domain.Services;

/// <summary>
/// Looks up existing contacts by normalized email or phone to prevent duplicates
/// during bulk imports and other automated ingestion paths.
/// </summary>
/// <remarks>Tenant and organization identifiers are represented as primitive <see cref="Guid"/> across the platform for cross-module simplicity. The returned contact id is the raw database id since the caller (import job) needs interoperability with EF-generated keys and the Contacts aggregate's strongly-typed <c>ContactId</c> wraps the same value.</remarks>
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

    /// <summary>
    /// Bulk-probes the Contacts table for any rows whose email matches one of the supplied
    /// (already-normalized) addresses. Returns a dictionary keyed by normalized email
    /// mapped to the existing contact id. Emails with no match are simply absent from the
    /// result. Designed to replace N+1 single-row lookups in import batches.
    /// </summary>
    /// <param name="tenantId">Tenant scope.</param>
    /// <param name="organizationId">Organization scope within the tenant.</param>
    /// <param name="normalizedEmails">Pre-normalized (lowercase + trimmed) email addresses.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, Guid>> FindExistingByEmailsAsync(
        Guid tenantId,
        Guid organizationId,
        IReadOnlyCollection<string> normalizedEmails,
        CancellationToken ct);
}
