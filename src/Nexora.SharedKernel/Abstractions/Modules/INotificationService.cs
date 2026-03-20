namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Cross-module interface for sending notifications.
/// Implemented by the Notifications module, consumed by other modules.
/// </summary>
public interface INotificationService
{
    /// <summary>Send a single notification to a contact using a template.</summary>
    Task<Guid> SendAsync(SendNotificationRequest request, CancellationToken ct = default);

    /// <summary>Send bulk notifications to multiple contacts.</summary>
    Task<Guid> SendBulkAsync(SendBulkNotificationRequest request, CancellationToken ct = default);

    /// <summary>Schedule a notification for future delivery.</summary>
    Task<Guid> ScheduleAsync(ScheduleNotificationRequest request, CancellationToken ct = default);
}

/// <summary>Request to send a single notification.</summary>
public sealed record SendNotificationRequest(
    string TemplateCode,
    string Channel,
    Guid ContactId,
    Dictionary<string, string> Variables,
    string? OrganizationId = null);

/// <summary>Request to send bulk notifications.</summary>
public sealed record SendBulkNotificationRequest(
    string TemplateCode,
    string Channel,
    IReadOnlyList<Guid> ContactIds,
    Dictionary<string, string> Variables,
    string? OrganizationId = null);

/// <summary>Request to schedule a notification for future delivery.</summary>
public sealed record ScheduleNotificationRequest(
    string TemplateCode,
    string Channel,
    Guid ContactId,
    Dictionary<string, string> Variables,
    DateTime ScheduledAt,
    string? OrganizationId = null);
