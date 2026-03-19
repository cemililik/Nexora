using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Bidirectional relationship between two contacts (e.g., parent-child, employer-employee).
/// </summary>
public sealed class ContactRelationship : Entity<ContactRelationshipId>
{
    public ContactId ContactId { get; private set; }
    public ContactId RelatedContactId { get; private set; }
    public RelationshipType Type { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ContactRelationship() { }

    public static ContactRelationship Create(
        ContactId contactId,
        ContactId relatedContactId,
        RelationshipType type)
    {
        return new ContactRelationship
        {
            Id = ContactRelationshipId.New(),
            ContactId = contactId,
            RelatedContactId = relatedContactId,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
