namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a new organization is created in the Identity module.</summary>
public sealed record OrganizationCreatedIntegrationEvent : IntegrationEventBase
{
    public required Guid OrganizationId { get; init; }
}
