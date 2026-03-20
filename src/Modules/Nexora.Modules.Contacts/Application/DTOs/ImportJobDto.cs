namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for a contact import job.</summary>
public sealed record ImportJobDto(
    Guid JobId,
    string Status,
    int TotalRows,
    int ProcessedRows,
    int SuccessCount,
    int ErrorCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
