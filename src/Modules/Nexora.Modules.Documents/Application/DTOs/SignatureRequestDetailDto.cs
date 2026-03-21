namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Detailed data transfer object for a signature request with recipients.</summary>
/// <param name="Id">Signature request identifier.</param>
/// <param name="DocumentId">Associated document identifier.</param>
/// <param name="Title">Title of the signature request.</param>
/// <param name="Status">Current status.</param>
/// <param name="ExpiresAt">Optional expiration date.</param>
/// <param name="CompletedAt">Completion timestamp, if completed.</param>
/// <param name="CreatedByUserId">User who created the request.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="Recipients">List of signature recipients.</param>
public sealed record SignatureRequestDetailDto(
    Guid Id,
    Guid DocumentId,
    string Title,
    string Status,
    DateOnly? ExpiresAt,
    DateTimeOffset? CompletedAt,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SignatureRecipientDto> Recipients);
