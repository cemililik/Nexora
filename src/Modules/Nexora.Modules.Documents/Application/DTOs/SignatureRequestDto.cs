namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Summary data transfer object for a signature request.</summary>
/// <param name="Id">Signature request identifier.</param>
/// <param name="DocumentId">Associated document identifier.</param>
/// <param name="Title">Title of the signature request.</param>
/// <param name="Status">Current status (Draft, Sent, PartiallySigned, Completed, Cancelled, Expired).</param>
/// <param name="ExpiresAt">Optional expiration date.</param>
/// <param name="RecipientCount">Number of recipients.</param>
/// <param name="SignedCount">Number of recipients who have signed.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
public sealed record SignatureRequestDto(
    Guid Id,
    Guid DocumentId,
    string Title,
    string Status,
    DateOnly? ExpiresAt,
    int RecipientCount,
    int SignedCount,
    DateTimeOffset CreatedAt);
