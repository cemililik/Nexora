namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a new user is created in the Identity module.</summary>
public sealed record UserCreatedIntegrationEvent : IntegrationEventBase
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}

/// <summary>Published when a new organization is created in the Identity module.</summary>
public sealed record OrganizationCreatedIntegrationEvent : IntegrationEventBase
{
    public required Guid OrganizationId { get; init; }
}
