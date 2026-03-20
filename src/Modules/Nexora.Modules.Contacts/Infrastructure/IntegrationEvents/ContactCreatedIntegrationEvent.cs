using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Published when a contact is created.</summary>
public sealed record ContactCreatedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
    public required string ContactType { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
}

// ConsentChangedIntegrationEvent is defined in Nexora.SharedKernel.Domain.Events
// for cross-module consumption by the Notifications module.
