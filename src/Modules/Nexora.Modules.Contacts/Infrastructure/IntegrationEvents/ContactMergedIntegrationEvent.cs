using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Published when two contacts are merged.</summary>
public sealed record ContactMergedIntegrationEvent : IntegrationEventBase
{
    public required Guid PrimaryContactId { get; init; }
    public required Guid SecondaryContactId { get; init; }
}
