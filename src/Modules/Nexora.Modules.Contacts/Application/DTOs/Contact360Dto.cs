using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>360-degree view DTO for a contact, aggregating all sub-entities and module summaries.</summary>
public sealed record Contact360Dto(
    ContactDetailDto Contact,
    IReadOnlyList<ContactRelationshipDto> Relationships,
    IReadOnlyList<CommunicationPreferenceDto> CommunicationPreferences,
    IReadOnlyList<ContactNoteDto> RecentNotes,
    IReadOnlyList<ConsentRecordDto> ConsentRecords,
    IReadOnlyList<ContactActivityDto> RecentActivities,
    IReadOnlyList<ContactCustomFieldDto> CustomFields,
    IReadOnlyList<ModuleContactSummary> ModuleSummaries);
