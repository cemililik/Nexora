using Nexora.Modules.Contacts.Domain.Events;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Tenant-wide tag definition. Tags are shared across organizations within a tenant
/// but tag assignments (ContactTag) are org-scoped.
/// </summary>
public sealed class Tag : AuditableEntity<TagId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Color { get; private set; }
    public TagCategory Category { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Tag() { }

    public static Tag Create(Guid tenantId, string name, TagCategory category, string? color = null)
    {
        var tag = new Tag
        {
            Id = TagId.New(),
            TenantId = tenantId,
            Name = name.Trim(),
            Category = category,
            Color = color
        };

        tag.AddDomainEvent(new TagCreatedEvent(tag.Id, tag.Name));

        return tag;
    }

    public void Update(string name, TagCategory category, string? color)
    {
        Name = name.Trim();
        Category = category;
        Color = color;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
