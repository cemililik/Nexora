using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a user is linked to a contact record.</summary>
public sealed record UserContactLinkedDomainEvent(
    UserId UserId,
    TenantId TenantId,
    Guid ContactId,
    Guid LinkedByUserId) : DomainEventBase;
