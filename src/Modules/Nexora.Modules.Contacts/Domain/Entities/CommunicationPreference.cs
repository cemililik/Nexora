using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Communication channel preference for a contact (opt-in/opt-out tracking).
/// </summary>
public sealed class CommunicationPreference : AuditableEntity<CommunicationPreferenceId>
{
    public ContactId ContactId { get; private set; }
    public CommunicationChannel Channel { get; private set; }
    public bool OptedIn { get; private set; }
    public DateTimeOffset? OptedInAt { get; private set; }
    public DateTimeOffset? OptedOutAt { get; private set; }
    public string? OptInSource { get; private set; }

    private CommunicationPreference() { }

    public static CommunicationPreference Create(
        ContactId contactId,
        CommunicationChannel channel,
        bool optedIn,
        string? optInSource = null)
    {
        var pref = new CommunicationPreference
        {
            Id = CommunicationPreferenceId.New(),
            ContactId = contactId,
            Channel = channel,
            OptedIn = optedIn,
            OptInSource = optInSource
        };

        if (optedIn)
            pref.OptedInAt = DateTimeOffset.UtcNow;
        else
            pref.OptedOutAt = DateTimeOffset.UtcNow;

        return pref;
    }

    public void OptIn(string? source = null)
    {
        OptedIn = true;
        OptedInAt = DateTimeOffset.UtcNow;
        OptedOutAt = null;
        OptInSource = source;
    }

    public void OptOut()
    {
        OptedIn = false;
        OptedOutAt = DateTimeOffset.UtcNow;
    }
}
