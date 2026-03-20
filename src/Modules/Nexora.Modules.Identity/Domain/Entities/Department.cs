using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a department within an organization.</summary>
public sealed class Department : AuditableEntity<DepartmentId>
{
    public OrganizationId OrganizationId { get; private set; }
    public DepartmentId? ParentDepartmentId { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    private Department() { }

    /// <summary>Creates a new department for an organization.</summary>
    public static Department Create(OrganizationId organizationId, string name, DepartmentId? parentId = null)
    {
        return new Department
        {
            Id = DepartmentId.New(),
            OrganizationId = organizationId,
            ParentDepartmentId = parentId,
            Name = name
        };
    }
}
