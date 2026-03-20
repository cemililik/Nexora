using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Custom field value for a contact, linked to a CustomFieldDefinition.
/// </summary>
public sealed class ContactCustomField : Entity<ContactCustomFieldId>
{
    public ContactId ContactId { get; private set; }
    public CustomFieldDefinitionId FieldDefinitionId { get; private set; }
    public string? Value { get; private set; }

    private ContactCustomField() { }

    /// <summary>Creates a new custom field value for a contact.</summary>
    public static ContactCustomField Create(
        ContactId contactId,
        CustomFieldDefinitionId fieldDefinitionId,
        string? value)
    {
        return new ContactCustomField
        {
            Id = ContactCustomFieldId.New(),
            ContactId = contactId,
            FieldDefinitionId = fieldDefinitionId,
            Value = value
        };
    }

    /// <summary>Updates the custom field value.</summary>
    public void UpdateValue(string? value)
    {
        Value = value;
    }
}
