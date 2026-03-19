namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Cross-module interface for querying contact data.
/// Implemented by Contacts module, consumed by other modules (CRM, Donations, etc.).
/// </summary>
public interface IContactQueryService
{
    /// <summary>Gets a contact summary by ID.</summary>
    Task<ContactSummary?> GetByIdAsync(Guid contactId, CancellationToken ct = default);

    /// <summary>Gets contact summaries by IDs.</summary>
    Task<IReadOnlyList<ContactSummary>> GetByIdsAsync(IEnumerable<Guid> contactIds, CancellationToken ct = default);

    /// <summary>Searches contacts by name or email within a tenant.</summary>
    Task<IReadOnlyList<ContactSummary>> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default);
}

/// <summary>
/// Lightweight contact data exposed to other modules via SharedKernel.
/// </summary>
public sealed record ContactSummary(
    Guid Id,
    string DisplayName,
    string? Email,
    string? Phone,
    string Type,
    string Status);
