namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>DTO for notification recipient delivery status.</summary>
public sealed record NotificationRecipientDto(
    Guid Id,
    Guid ContactId,
    string RecipientAddress,
    string Status,
    string? ProviderMessageId,
    string? FailureReason,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    DateTime? OpenedAt);
