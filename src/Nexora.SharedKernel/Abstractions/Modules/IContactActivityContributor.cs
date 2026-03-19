namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Interface for modules to contribute activity summaries to the contact 360-degree view.
/// Each module (CRM, Donations, Education, etc.) implements this to provide its data.
/// </summary>
public interface IContactActivityContributor
{
    /// <summary>The module name (e.g., "crm", "donations").</summary>
    string ModuleName { get; }

    /// <summary>Gets a summary of module-specific data for a contact.</summary>
    Task<ModuleContactSummary?> GetSummaryAsync(Guid contactId, Guid organizationId, CancellationToken ct = default);
}

/// <summary>
/// Module-specific summary data for a contact (displayed in 360-degree view).
/// </summary>
public sealed record ModuleContactSummary(
    string ModuleName,
    string DisplayName,
    IReadOnlyDictionary<string, object?> Data);
