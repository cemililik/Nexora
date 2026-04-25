using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

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

    /// <summary>
    /// Activates the tenant. No-op if already active.
    /// Only Trial and Suspended tenants can be activated.
    /// </summary>
    public void Activate()
    {
        if (Status == TenantStatus.Active) return;

        // Trial and Suspended are operator-driven entry paths.
        // MigrationFailed is the recovery path after ops fixes a failed
        // migration and reruns `platform:retry-tenant-migration` per the
        // migration-orchestration runbook — re-activating here lifts the
        // 503 quarantine.
        if (Status is not (TenantStatus.Trial or TenantStatus.Suspended or TenantStatus.MigrationFailed))
            throw new DomainException("lockey_identity_error_tenant_activation_not_allowed");

        Status = TenantStatus.Active;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Active));
    }

    /// <summary>
    /// Suspends the tenant. No-op if already suspended.
    /// Only Active tenants can be suspended.
    /// </summary>
    public void Suspend()
    {
        if (Status == TenantStatus.Suspended) return;

        if (Status is not TenantStatus.Active)
            throw new DomainException("lockey_identity_error_tenant_suspension_not_allowed");

        Status = TenantStatus.Suspended;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Suspended));
    }

    /// <summary>
    /// Quarantines the tenant after a migration failure. Set by
    /// <c>IMigrationRunner</c> (see T-011) when a per-module
    /// <c>MigrateAsync</c> throws — the API layer rejects subsequent
    /// requests with HTTP 503 until ops manually retries via
    /// <c>platform:retry-tenant-migration</c> per
    /// <c>docs/operations/migration-orchestration.md</c> §2.3. No-op
    /// if already in <see cref="TenantStatus.MigrationFailed"/>; only
    /// non-terminated tenants can transition (a Terminated tenant
    /// stays Terminated).
    /// </summary>
    public void MarkMigrationFailed()
    {
        if (Status == TenantStatus.MigrationFailed) return;
        if (Status == TenantStatus.Terminated)
            throw new DomainException("lockey_identity_error_tenant_migration_failed_not_allowed");
        Status = TenantStatus.MigrationFailed;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.MigrationFailed));
    }

    /// <summary>
    /// Terminates the tenant. No-op if already terminated.
    /// Any non-terminated tenant can be terminated.
    /// </summary>
    public void Terminate()
    {
        if (Status == TenantStatus.Terminated) return;
        Status = TenantStatus.Terminated;
        AddDomainEvent(new TenantStatusChangedEvent(Id, TenantStatus.Terminated));
    }

    /// <summary>Sets the Keycloak realm identifier for this tenant.</summary>
    public void SetRealmId(string realmId)
    {
        if (string.IsNullOrWhiteSpace(realmId))
            throw new DomainException("lockey_identity_error_realm_id_required");

        RealmId = realmId.Trim();
    }

    /// <summary>Persists the given <see cref="TenantSettings"/> to the JSONB Settings column.</summary>
    public void UpdateSettings(TenantSettings settings)
    {
        Settings = settings.ToJson();
    }

    /// <summary>
    /// Deserializes and returns the current <see cref="TenantSettings"/>.
    /// Returns <see cref="TenantSettings.Default"/> when Settings is null or malformed.
    /// </summary>
    public TenantSettings GetSettings() => TenantSettings.FromJson(Settings);
}

/// <summary>Represents the lifecycle status of a tenant.</summary>
public enum TenantStatus
{
    /// <summary>Limited-time onboarding period before full activation.</summary>
    Trial,

    /// <summary>Fully provisioned and allowed access.</summary>
    Active,

    /// <summary>Access temporarily revoked; can be reactivated.</summary>
    Suspended,

    /// <summary>Account permanently closed; terminal state.</summary>
    Terminated,

    /// <summary>
    /// A platform migration (per ADR-0027 / T-011 <c>MigrationRunner</c>) failed
    /// for this tenant — API layer returns 503 until ops manually retries.
    /// Distinct from Suspended (which is an operator action, not a system one).
    /// </summary>
    MigrationFailed
}
