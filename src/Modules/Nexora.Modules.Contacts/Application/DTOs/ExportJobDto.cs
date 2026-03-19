namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for a contact export job.</summary>
public sealed record ExportJobDto(
    Guid JobId,
    string Status,
    string Format,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? DownloadUrl);
