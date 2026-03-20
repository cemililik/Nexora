using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a member is removed from an organization.</summary>
public sealed record OrganizationMemberRemovedEvent(OrganizationId OrganizationId, UserId UserId) : DomainEventBase;
