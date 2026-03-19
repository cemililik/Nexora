using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

public sealed record TenantCreatedEvent(TenantId TenantId, string Slug) : DomainEventBase;

public sealed record TenantStatusChangedEvent(TenantId TenantId, string NewStatus) : DomainEventBase;

public sealed record OrganizationCreatedEvent(OrganizationId OrganizationId, TenantId TenantId) : DomainEventBase;

public sealed record UserCreatedEvent(UserId UserId, TenantId TenantId, string Email) : DomainEventBase;

public sealed record UserDeactivatedEvent(UserId UserId) : DomainEventBase;

public sealed record RolePermissionChangedEvent(RoleId RoleId, string Action) : DomainEventBase;
