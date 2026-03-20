using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a new tenant is created.</summary>
public sealed record TenantCreatedEvent(TenantId TenantId, string Slug) : DomainEventBase;
