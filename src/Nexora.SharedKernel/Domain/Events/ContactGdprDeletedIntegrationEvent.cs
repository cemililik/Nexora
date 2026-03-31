namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a contact is GDPR-deleted, enabling cross-module PII cleanup.</summary>
public sealed record ContactGdprDeletedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
    public required string Reason { get; init; }
}
