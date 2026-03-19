using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Tenant-level custom field definition. Defines additional fields that can be set on contacts.
/// </summary>
public sealed class CustomFieldDefinition : AuditableEntity<CustomFieldDefinitionId>
{
    public Guid TenantId { get; private set; }
    public string FieldName { get; private set; } = default!;
    public string FieldType { get; private set; } = default!;
    public string? Options { get; private set; }
    public bool IsRequired { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CustomFieldDefinition() { }

    public static CustomFieldDefinition Create(
        Guid tenantId,
        string fieldName,
        string fieldType,
        string? options = null,
        bool isRequired = false,
        int displayOrder = 0)
    {
        return new CustomFieldDefinition
        {
            Id = CustomFieldDefinitionId.New(),
            TenantId = tenantId,
            FieldName = fieldName.Trim(),
            FieldType = fieldType.ToLowerInvariant(),
            Options = options,
            IsRequired = isRequired,
            DisplayOrder = displayOrder
        };
    }

    public void Update(string fieldName, string? options, bool isRequired, int displayOrder)
    {
        FieldName = fieldName.Trim();
        Options = options;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }

    public void Deactivate() => IsActive = false;
}
