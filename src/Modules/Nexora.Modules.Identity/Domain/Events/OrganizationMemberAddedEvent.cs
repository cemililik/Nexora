using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a member is added to an organization.</summary>
public sealed record OrganizationMemberAddedEvent(OrganizationId OrganizationId, UserId UserId) : DomainEventBase;
