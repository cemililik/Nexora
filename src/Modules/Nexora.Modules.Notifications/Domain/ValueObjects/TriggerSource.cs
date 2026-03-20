namespace Nexora.Modules.Notifications.Domain.ValueObjects;

/// <summary>Constants for notification trigger sources (the TriggeredBy field).</summary>
public static class TriggerSource
{
    /// <summary>Triggered by a single send API call.</summary>
    public const string Api = "api";

    /// <summary>Triggered by a bulk send API call.</summary>
    public const string BulkApi = "bulk_api";

    /// <summary>Triggered by a scheduled notification dispatcher.</summary>
    public const string Scheduled = "scheduled";
}
