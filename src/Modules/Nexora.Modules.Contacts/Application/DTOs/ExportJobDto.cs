namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for a contact export job.</summary>
public sealed record ExportJobDto(
    Guid JobId,
    string Status,
    string Format,
    int TotalRows,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? DownloadUrl,
    string? ErrorDetails);
