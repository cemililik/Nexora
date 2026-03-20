using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Aggregate root representing a digital signature request for a document.
/// Manages the lifecycle from draft through signing to completion.
/// </summary>
public sealed class SignatureRequest : AuditableEntity<SignatureRequestId>, IAggregateRoot
{
    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the organization identifier.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the document identifier being signed.</summary>
    public DocumentId DocumentId { get; private set; }

    /// <summary>Gets the identifier of the user who created the request.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Gets the title of the signature request.</summary>
    public string Title { get; private set; } = default!;

    /// <summary>Gets the current status of the signature request.</summary>
    public SignatureRequestStatus Status { get; private set; }

    /// <summary>Gets the expiration date, or null if no expiry.</summary>
    public DateOnly? ExpiresAt { get; private set; }

    /// <summary>Gets the completion timestamp, or null if not yet completed.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<SignatureRecipient> _recipients = [];

    /// <summary>Gets the collection of signature recipients.</summary>
    public IReadOnlyList<SignatureRecipient> Recipients => _recipients.AsReadOnly();

    private SignatureRequest() { }

    /// <summary>Creates a new SignatureRequest instance.</summary>
    public static SignatureRequest Create(
        Guid tenantId,
        Guid organizationId,
        DocumentId documentId,
        Guid createdByUserId,
        string title,
        DateOnly? expiresAt = null)
    {
        return new SignatureRequest
        {
            Id = SignatureRequestId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            DocumentId = documentId,
            CreatedByUserId = createdByUserId,
            Title = title.Trim(),
            Status = SignatureRequestStatus.Draft,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>Adds a recipient to the signature request.</summary>
    public void AddRecipient(Guid contactId, string email, string name, int signingOrder)
    {
        if (Status is not SignatureRequestStatus.Draft)
            throw new DomainException("lockey_documents_error_cannot_add_recipient_after_sent");

        var recipient = SignatureRecipient.Create(Id, contactId, email, name, signingOrder);
        _recipients.Add(recipient);
    }

    /// <summary>Sends the signature request to all recipients.</summary>
    public void Send()
    {
        if (Status is not SignatureRequestStatus.Draft)
            throw new DomainException("lockey_documents_error_only_draft_can_send");

        if (_recipients.Count == 0)
            throw new DomainException("lockey_documents_error_no_recipients");

        Status = SignatureRequestStatus.Sent;
        AddDomainEvent(new SignatureRequestSentEvent(Id, DocumentId));
    }

    /// <summary>Records a signature from a recipient.</summary>
    public void RecordSignature(SignatureRecipientId recipientId, string signatureData, string ipAddress)
    {
        if (Status is not (SignatureRequestStatus.Sent or SignatureRequestStatus.PartiallySigned))
            throw new DomainException("lockey_documents_error_cannot_sign_in_current_status");

        if (ExpiresAt is not null && ExpiresAt <= DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("lockey_documents_error_signature_request_expired");

        var recipient = _recipients.FirstOrDefault(r => r.Id == recipientId)
            ?? throw new DomainException("lockey_documents_error_recipient_not_found");

        recipient.Sign(signatureData, ipAddress);
        AddDomainEvent(new DocumentSignedEvent(Id, recipientId));

        if (_recipients.All(r => r.Status == SignatureRecipientStatus.Signed))
        {
            Status = SignatureRequestStatus.Completed;
            CompletedAt = DateTimeOffset.UtcNow;
            AddDomainEvent(new SignatureCompletedEvent(Id, DocumentId));
        }
        else if (Status is SignatureRequestStatus.Sent)
        {
            Status = SignatureRequestStatus.PartiallySigned;
        }
    }

    /// <summary>Cancels the signature request.</summary>
    public void Cancel()
    {
        if (Status is SignatureRequestStatus.Completed or SignatureRequestStatus.Expired)
            throw new DomainException("lockey_documents_error_cannot_cancel_completed_or_expired");

        Status = SignatureRequestStatus.Cancelled;
    }

    /// <summary>Expires the signature request and pending recipients.</summary>
    public void Expire()
    {
        if (Status is SignatureRequestStatus.Completed or SignatureRequestStatus.Cancelled)
            return;

        Status = SignatureRequestStatus.Expired;
        foreach (var recipient in _recipients.Where(r => r.Status is SignatureRecipientStatus.Pending or SignatureRecipientStatus.Viewed))
            recipient.MarkExpired();
    }
}
