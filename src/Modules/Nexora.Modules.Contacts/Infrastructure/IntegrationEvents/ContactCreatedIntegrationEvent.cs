using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Published when a contact is created.</summary>
public sealed record ContactCreatedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the unique identifier of the created contact.</summary>
    public required Guid ContactId { get; init; }

    /// <summary>Gets the type of the contact (e.g., Individual, Organization).</summary>
    public required string ContactType { get; init; }

    /// <summary>Gets the email address of the contact, if available.</summary>
    public string? Email { get; init; }

    /// <summary>Gets the display name of the contact.</summary>
    public required string DisplayName { get; init; }
}

// ConsentChangedIntegrationEvent is defined in Nexora.SharedKernel.Domain.Events
// for cross-module consumption by the Notifications module.
