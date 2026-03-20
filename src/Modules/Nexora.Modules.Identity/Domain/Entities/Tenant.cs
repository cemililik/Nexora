using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a tenant in the multi-tenant platform.</summary>
public sealed class Tenant : AuditableEntity<TenantId>, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? RealmId { get; private set; }
    public TenantStatus Status { get; private set; }
    public string? Settings { get; private set; }

    private readonly List<Organization> _organizations = [];
    public IReadOnlyList<Organization> Organizations => _organizations.AsReadOnly();

    private readonly List<TenantModule> _modules = [];
    public IReadOnlyList<TenantModule> Modules => _modules.AsReadOnly();

    private Tenant() { }

    /// <summary>Creates a new tenant with trial status.</summary>
    public static Tenant Create(string name, string slug)
    {
        var tenant = new Tenant
        {
            Id = TenantId.New(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            Status = TenantStatus.Trial
        };
        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.Slug));
        return tenant;
    }

    /// <summary>Activates the tenant.</summary>
    public void Activate() { Status = TenantStatus.Active; AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Active)); }
    /// <summary>Suspends the tenant.</summary>
    public void Suspend() { Status = TenantStatus.Suspended; AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Suspended)); }
    /// <summary>Terminates the tenant.</summary>
    public void Terminate() { Status = TenantStatus.Terminated; AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Terminated)); }
    /// <summary>Sets the Keycloak realm identifier for this tenant.</summary>
    public void SetRealmId(string realmId) => RealmId = realmId;
}

/// <summary>Represents the lifecycle status of a tenant.</summary>
public enum TenantStatus
{
    Trial,
    Active,
    Suspended,
    Terminated
}
