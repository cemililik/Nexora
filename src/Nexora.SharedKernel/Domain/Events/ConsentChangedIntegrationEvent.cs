namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a contact's consent changes in the Contacts module.</summary>
public sealed record ConsentChangedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
    public required string ConsentType { get; init; }
    public required bool Granted { get; init; }
}
