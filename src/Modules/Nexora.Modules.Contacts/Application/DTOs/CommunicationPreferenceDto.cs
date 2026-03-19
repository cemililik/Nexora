namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for communication preference.</summary>
public sealed record CommunicationPreferenceDto(
    Guid Id,
    Guid ContactId,
    string Channel,
    bool OptedIn,
    DateTimeOffset? OptedInAt,
    DateTimeOffset? OptedOutAt,
    string? OptInSource);
