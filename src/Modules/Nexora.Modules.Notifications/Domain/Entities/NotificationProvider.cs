using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Domain.Entities;

/// <summary>
/// Aggregate root representing a tenant-level notification provider configuration.
/// Stores provider credentials (encrypted) and daily sending limits.
/// </summary>
public sealed class NotificationProvider : AuditableEntity<NotificationProviderId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public ProviderName ProviderName { get; private set; }
    public string Config { get; private set; } = default!;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public int DailyLimit { get; private set; }
    public int SentToday { get; private set; }

    private NotificationProvider() { }

    /// <summary>Creates a new notification provider configuration for the specified tenant and channel.</summary>
    public static NotificationProvider Create(
        Guid tenantId,
        NotificationChannel channel,
        ProviderName providerName,
        string config,
        int dailyLimit,
        bool isDefault = false)
    {
        return new NotificationProvider
        {
            Id = NotificationProviderId.New(),
            TenantId = tenantId,
            Channel = channel,
            ProviderName = providerName,
            Config = config,
            IsDefault = isDefault,
            IsActive = true,
            DailyLimit = dailyLimit,
            SentToday = 0
        };
    }

    /// <summary>Updates the provider configuration, daily limit, and default flag.</summary>
    public void Update(string config, int dailyLimit, bool isDefault)
    {
        Config = config;
        DailyLimit = dailyLimit;
        IsDefault = isDefault;
    }

    /// <summary>Activates the provider so it can be used for sending.</summary>
    public void Activate()
    {
        if (IsActive)
            throw new DomainException("lockey_notifications_error_provider_already_active");

        IsActive = true;
    }

    /// <summary>Deactivates the provider, preventing it from being used.</summary>
    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("lockey_notifications_error_provider_already_inactive");

        IsActive = false;
    }

    /// <summary>Increments the daily sent counter, enforcing the daily limit.</summary>
    public void IncrementSentToday(int count = 1)
    {
        if (SentToday + count > DailyLimit)
            throw new DomainException("lockey_notifications_error_provider_daily_limit_exceeded");

        SentToday += count;
    }

    /// <summary>Resets the daily sent counter to zero.</summary>
    public void ResetDailyCounter()
    {
        SentToday = 0;
    }

    /// <summary>Checks whether the provider has remaining daily capacity for the specified count.</summary>
    public bool HasDailyCapacity(int count = 1) => SentToday + count <= DailyLimit;
}
