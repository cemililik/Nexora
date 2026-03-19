using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Contacts.Infrastructure;

/// <summary>
/// Aggregates module summaries from all registered IContactActivityContributor implementations.
/// Each installed module provides its own contributor.
/// </summary>
public sealed class ContactActivityContributorAggregator(
    IEnumerable<IContactActivityContributor> contributors)
{
    /// <summary>Collects summaries from all registered contributors for a contact.</summary>
    public async Task<IReadOnlyList<ModuleContactSummary>> GetAllSummariesAsync(
        Guid contactId, Guid organizationId, CancellationToken ct)
    {
        var summaries = new List<ModuleContactSummary>();

        foreach (var contributor in contributors)
        {
            var summary = await contributor.GetSummaryAsync(contactId, organizationId, ct);
            if (summary is not null)
                summaries.Add(summary);
        }

        return summaries;
    }
}
