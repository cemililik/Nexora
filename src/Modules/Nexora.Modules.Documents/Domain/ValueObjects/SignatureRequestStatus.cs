namespace Nexora.Modules.Documents.Domain.ValueObjects;

/// <summary>Status of a digital signature request.</summary>
public enum SignatureRequestStatus
{
    /// <summary>Request is in draft.</summary>
    Draft,

    /// <summary>Request has been sent.</summary>
    Sent,

    /// <summary>Some recipients have signed.</summary>
    PartiallySigned,

    /// <summary>All recipients have signed.</summary>
    Completed,

    /// <summary>Request was cancelled.</summary>
    Cancelled,

    /// <summary>Request has expired.</summary>
    Expired
}
