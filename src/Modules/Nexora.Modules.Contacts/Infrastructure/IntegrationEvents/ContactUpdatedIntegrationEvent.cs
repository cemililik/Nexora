using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Published when a contact is updated.</summary>
public sealed record ContactUpdatedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
}
