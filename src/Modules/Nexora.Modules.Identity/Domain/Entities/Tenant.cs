using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a tenant in the multi-tenant platform.</summary>
public sealed class Tenant : AuditableEntity<TenantId>, IAggregateRoot
{
    /// <summary>Gets the display name of the tenant.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Gets the URL-friendly slug identifier.</summary>
    public string Slug { get; private set; } = default!;

    /// <summary>Gets the Keycloak realm identifier, if configured.</summary>
    public string? RealmId { get; private set; }

    /// <summary>Gets the current lifecycle status of the tenant.</summary>
    public TenantStatus Status { get; private set; }

    /// <summary>Gets the JSON-serialized tenant settings.</summary>
    public string? Settings { get; private set; }

    private readonly List<Organization> _organizations = [];

    /// <summary>Gets the organizations belonging to this tenant.</summary>
    public IReadOnlyList<Organization> Organizations => _organizations.AsReadOnly();

    private readonly List<TenantModule> _modules = [];

    /// <summary>Gets the modules installed for this tenant.</summary>
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

    /// <summary>Activates the tenant. No-op if already active.</summary>
    public void Activate()
    {
        if (Status == TenantStatus.Active) return;
        Status = TenantStatus.Active;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Active));
    }

    /// <summary>Suspends the tenant. No-op if already suspended.</summary>
    public void Suspend()
    {
        if (Status == TenantStatus.Suspended) return;
        Status = TenantStatus.Suspended;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Suspended));
    }

    /// <summary>Terminates the tenant. No-op if already terminated.</summary>
    public void Terminate()
    {
        if (Status == TenantStatus.Terminated) return;
        Status = TenantStatus.Terminated;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Terminated));
    }
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
