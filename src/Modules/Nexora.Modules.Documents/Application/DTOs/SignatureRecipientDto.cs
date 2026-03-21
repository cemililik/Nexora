namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for a signature recipient.</summary>
/// <param name="Id">Recipient identifier.</param>
/// <param name="ContactId">Contact identifier.</param>
/// <param name="Email">Recipient email address.</param>
/// <param name="Name">Recipient display name.</param>
/// <param name="SigningOrder">Order position for signing.</param>
/// <param name="Status">Current signing status (Pending, Viewed, Signed, Declined, Expired).</param>
/// <param name="SignedAt">Timestamp when the recipient signed, or null.</param>
public sealed record SignatureRecipientDto(
    Guid Id,
    Guid ContactId,
    string Email,
    string Name,
    int SigningOrder,
    string Status,
    DateTimeOffset? SignedAt);
