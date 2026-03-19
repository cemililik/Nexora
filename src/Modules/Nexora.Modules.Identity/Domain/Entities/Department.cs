using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

public sealed class Department : AuditableEntity<DepartmentId>
{
    public OrganizationId OrganizationId { get; private set; }
    public DepartmentId? ParentDepartmentId { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    private Department() { }

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
