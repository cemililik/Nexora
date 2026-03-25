using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Reporting.Domain.Entities;

/// <summary>
/// User-defined dashboard composed of report-driven widgets.
/// </summary>
public sealed class Dashboard : AuditableEntity<DashboardId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public string? Widgets { get; private set; } // JSON array of DashboardWidget

    private Dashboard() { }

    public static Dashboard Create(
        Guid tenantId,
        Guid organizationId,
        string name,
        string? description,
        bool isDefault = false)
    {
        return new Dashboard
        {
            Id = DashboardId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsDefault = isDefault
        };
    }

    public void Update(string name, string? description, string? widgets, bool isDefault)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Widgets = widgets;
        IsDefault = isDefault;
    }
}
