using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Published when a contact is archived.</summary>
public sealed record ContactArchivedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
}
