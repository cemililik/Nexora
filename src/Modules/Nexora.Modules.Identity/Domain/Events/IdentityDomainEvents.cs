using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a new tenant is created.</summary>
public sealed record TenantCreatedEvent(TenantId TenantId, string Slug) : DomainEventBase;

/// <summary>Raised when a tenant's status changes (activate, suspend, terminate).</summary>
public sealed record TenantStatusChangedEvent(TenantId TenantId, string NewStatus) : DomainEventBase;

/// <summary>Raised when a new organization is created within a tenant.</summary>
public sealed record OrganizationCreatedEvent(OrganizationId OrganizationId, TenantId TenantId) : DomainEventBase;

/// <summary>Raised when a new user is created.</summary>
public sealed record UserCreatedEvent(UserId UserId, TenantId TenantId, string Email) : DomainEventBase;

/// <summary>Raised when a user is deactivated.</summary>
public sealed record UserDeactivatedEvent(UserId UserId) : DomainEventBase;

/// <summary>Raised when a member is added to an organization.</summary>
public sealed record OrganizationMemberAddedEvent(OrganizationId OrganizationId, UserId UserId) : DomainEventBase;

/// <summary>Raised when a member is removed from an organization.</summary>
public sealed record OrganizationMemberRemovedEvent(OrganizationId OrganizationId, UserId UserId) : DomainEventBase;

/// <summary>Raised when role permissions are changed.</summary>
public sealed record RolePermissionChangedEvent(RoleId RoleId, string Action) : DomainEventBase;
