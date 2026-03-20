namespace Nexora.Modules.Notifications.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing a notification template.</summary>
public readonly record struct NotificationTemplateId(Guid Value)
{
    public static NotificationTemplateId New() => new(Guid.NewGuid());
    public static NotificationTemplateId From(Guid value) => new(value);
    public static NotificationTemplateId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a notification template translation.</summary>
public readonly record struct NotificationTemplateTranslationId(Guid Value)
{
    public static NotificationTemplateTranslationId New() => new(Guid.NewGuid());
    public static NotificationTemplateTranslationId From(Guid value) => new(value);
    public static NotificationTemplateTranslationId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a sent notification.</summary>
public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
    public static NotificationId From(Guid value) => new(value);
    public static NotificationId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a notification recipient.</summary>
public readonly record struct NotificationRecipientId(Guid Value)
{
    public static NotificationRecipientId New() => new(Guid.NewGuid());
    public static NotificationRecipientId From(Guid value) => new(value);
    public static NotificationRecipientId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a notification provider configuration.</summary>
public readonly record struct NotificationProviderId(Guid Value)
{
    public static NotificationProviderId New() => new(Guid.NewGuid());
    public static NotificationProviderId From(Guid value) => new(value);
    public static NotificationProviderId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a scheduled notification.</summary>
public readonly record struct NotificationScheduleId(Guid Value)
{
    public static NotificationScheduleId New() => new(Guid.NewGuid());
    public static NotificationScheduleId From(Guid value) => new(value);
    public static NotificationScheduleId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
