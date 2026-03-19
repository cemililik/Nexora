using Nexora.Modules.Contacts.Domain.Events;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Records consent grants and revocations for GDPR/KVKK compliance.
/// Consent records are append-only; revocations create a new entry.
/// </summary>
public sealed class ConsentRecord : Entity<ConsentRecordId>
{
    public ContactId ContactId { get; private set; }
    public ConsentType ConsentType { get; private set; }
    public bool Granted { get; private set; }
    public string? Source { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private ConsentRecord() { }

    public static ConsentRecord Create(
        ContactId contactId,
        ConsentType consentType,
        bool granted,
        string? source = null,
        string? ipAddress = null)
    {
        var record = new ConsentRecord
        {
            Id = ConsentRecordId.New(),
            ContactId = contactId,
            ConsentType = consentType,
            Granted = granted,
            Source = source,
            IpAddress = ipAddress,
            GrantedAt = DateTimeOffset.UtcNow
        };
        record.AddDomainEvent(new ConsentChangedEvent(contactId, consentType, granted));
        return record;
    }

    public void Revoke()
    {
        Granted = false;
        RevokedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new ConsentChangedEvent(ContactId, ConsentType, false));
    }
}
