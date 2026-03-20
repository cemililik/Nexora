namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing a contact.</summary>
public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.NewGuid());
    public static ContactId From(Guid value) => new(value);
    public static ContactId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a contact address.</summary>
public readonly record struct ContactAddressId(Guid Value)
{
    public static ContactAddressId New() => new(Guid.NewGuid());
    public static ContactAddressId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a tag.</summary>
public readonly record struct TagId(Guid Value)
{
    public static TagId New() => new(Guid.NewGuid());
    public static TagId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a contact-tag assignment.</summary>
public readonly record struct ContactTagId(Guid Value)
{
    public static ContactTagId New() => new(Guid.NewGuid());
    public static ContactTagId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a contact relationship.</summary>
public readonly record struct ContactRelationshipId(Guid Value)
{
    public static ContactRelationshipId New() => new(Guid.NewGuid());
    public static ContactRelationshipId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a communication preference.</summary>
public readonly record struct CommunicationPreferenceId(Guid Value)
{
    public static CommunicationPreferenceId New() => new(Guid.NewGuid());
    public static CommunicationPreferenceId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a contact note.</summary>
public readonly record struct ContactNoteId(Guid Value)
{
    public static ContactNoteId New() => new(Guid.NewGuid());
    public static ContactNoteId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a custom field definition.</summary>
public readonly record struct CustomFieldDefinitionId(Guid Value)
{
    public static CustomFieldDefinitionId New() => new(Guid.NewGuid());
    public static CustomFieldDefinitionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a contact custom field value.</summary>
public readonly record struct ContactCustomFieldId(Guid Value)
{
    public static ContactCustomFieldId New() => new(Guid.NewGuid());
    public static ContactCustomFieldId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a consent record.</summary>
public readonly record struct ConsentRecordId(Guid Value)
{
    public static ConsentRecordId New() => new(Guid.NewGuid());
    public static ConsentRecordId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a contact activity entry.</summary>
public readonly record struct ContactActivityId(Guid Value)
{
    public static ContactActivityId New() => new(Guid.NewGuid());
    public static ContactActivityId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
