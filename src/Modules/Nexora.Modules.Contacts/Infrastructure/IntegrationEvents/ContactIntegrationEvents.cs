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

/// <summary>Published when a contact is updated.</summary>
public sealed record ContactUpdatedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
}

/// <summary>Published when two contacts are merged.</summary>
public sealed record ContactMergedIntegrationEvent : IntegrationEventBase
{
    public required Guid PrimaryContactId { get; init; }
    public required Guid SecondaryContactId { get; init; }
}

/// <summary>Published when a contact is archived.</summary>
public sealed record ContactArchivedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
}

/// <summary>Published when a contact's consent changes.</summary>
public sealed record ConsentChangedIntegrationEvent : IntegrationEventBase
{
    public required Guid ContactId { get; init; }
    public required string ConsentType { get; init; }
    public required bool Granted { get; init; }
}
