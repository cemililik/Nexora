using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a new organization is created within a tenant.</summary>
public sealed record OrganizationCreatedEvent(OrganizationId OrganizationId, TenantId TenantId) : DomainEventBase;
