namespace Nexora.Modules.Documents.Domain.ValueObjects;

/// <summary>Status of a signature recipient in the signing flow.</summary>
public enum SignatureRecipientStatus
{
    /// <summary>Awaiting recipient action.</summary>
    Pending,

    /// <summary>Recipient has viewed request.</summary>
    Viewed,

    /// <summary>Recipient has signed.</summary>
    Signed,

    /// <summary>Recipient declined to sign.</summary>
    Declined,

    /// <summary>Recipient signing has expired.</summary>
    Expired
}
