using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

public sealed class Organization : AuditableEntity<OrganizationId>, IAggregateRoot
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? LogoUrl { get; private set; }
    public string Timezone { get; private set; } = "UTC";
    public string DefaultCurrency { get; private set; } = "USD";
    public string DefaultLanguage { get; private set; } = "en";
    public bool IsActive { get; private set; } = true;

    private readonly List<Department> _departments = [];
    public IReadOnlyList<Department> Departments => _departments.AsReadOnly();

    private Organization() { }

    public static Organization Create(TenantId tenantId, string name, string slug)
    {
        var org = new Organization
        {
            Id = OrganizationId.New(),
            TenantId = tenantId,
            Name = name,
            Slug = slug.ToLowerInvariant()
        };
        org.AddDomainEvent(new OrganizationCreatedEvent(org.Id, tenantId));
        return org;
    }

    public void Update(string name, string timezone, string currency, string language)
    {
        Name = name;
        Timezone = timezone;
        DefaultCurrency = currency;
        DefaultLanguage = language;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
