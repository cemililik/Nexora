using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Represents a recipient in a digital signature request with signing status tracking.
/// </summary>
public sealed class SignatureRecipient : AuditableEntity<SignatureRecipientId>
{
    /// <summary>Gets the parent signature request identifier.</summary>
    public SignatureRequestId RequestId { get; private set; }

    /// <summary>Gets the contact identifier.</summary>
    public Guid ContactId { get; private set; }

    /// <summary>Gets the recipient email address.</summary>
    public string Email { get; private set; } = default!;

    /// <summary>Gets the recipient display name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Gets the signing order position.</summary>
    public int SigningOrder { get; private set; }

    /// <summary>Gets the current signing status.</summary>
    public SignatureRecipientStatus Status { get; private set; }

    /// <summary>Gets the signature data, or null if not yet signed.</summary>
    public string? SignatureData { get; private set; }

    /// <summary>Gets the IP address used when signing, or null if not yet signed.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the timestamp when the recipient signed, or null if not yet signed.</summary>
    public DateTimeOffset? SignedAt { get; private set; }

    private SignatureRecipient() { }

    /// <summary>Creates a new SignatureRecipient instance.</summary>
    public static SignatureRecipient Create(
        SignatureRequestId requestId,
        Guid contactId,
        string email,
        string name,
        int signingOrder)
    {
        return new SignatureRecipient
        {
            Id = SignatureRecipientId.New(),
            RequestId = requestId,
            ContactId = contactId,
            Email = email.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            SigningOrder = signingOrder,
            Status = SignatureRecipientStatus.Pending
        };
    }

    /// <summary>Marks the recipient as having viewed the request.</summary>
    public void MarkViewed()
    {
        if (Status is SignatureRecipientStatus.Pending)
            Status = SignatureRecipientStatus.Viewed;
    }

    /// <summary>Records the recipient's signature.</summary>
    public void Sign(string signatureData, string ipAddress)
    {
        if (Status is SignatureRecipientStatus.Signed)
            throw new DomainException("lockey_documents_error_already_signed");

        if (Status is SignatureRecipientStatus.Declined or SignatureRecipientStatus.Expired)
            throw new DomainException("lockey_documents_error_cannot_sign");

        SignatureData = signatureData;
        IpAddress = ipAddress;
        SignedAt = DateTimeOffset.UtcNow;
        Status = SignatureRecipientStatus.Signed;
    }

    /// <summary>Declines the signing request.</summary>
    public void Decline()
    {
        if (Status is not (SignatureRecipientStatus.Pending or SignatureRecipientStatus.Viewed))
            throw new DomainException("lockey_documents_error_cannot_decline");

        Status = SignatureRecipientStatus.Declined;
    }

    /// <summary>Marks the recipient as expired.</summary>
    public void MarkExpired()
    {
        Status = SignatureRecipientStatus.Expired;
    }
}
