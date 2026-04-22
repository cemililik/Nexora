using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a user's contact link is removed.</summary>
public sealed record UserContactUnlinkedDomainEvent(
    UserId UserId,
    TenantId TenantId,
    Guid PreviousContactId) : DomainEventBase;
