namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Provides contextual information about the current user and request for audit logging.
/// </summary>
public interface IAuditContext
{
    /// <summary>The authenticated user ID, if available.</summary>
    Guid? UserId { get; }

    /// <summary>The email of the authenticated user, if available.</summary>
    string? UserEmail { get; }

    /// <summary>The client IP address.</summary>
    string? IpAddress { get; }

    /// <summary>The client User-Agent header value.</summary>
    string? UserAgent { get; }

    /// <summary>The correlation identifier for the current request.</summary>
    string? CorrelationId { get; }
}
