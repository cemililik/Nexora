namespace Nexora.Modules.Notifications.Domain.ValueObjects;

/// <summary>Supported notification delivery provider names.</summary>
public enum ProviderName
{
    SendGrid,
    Mailgun,
    Twilio,
    Netgsm,
    WhatsAppBusiness
}
