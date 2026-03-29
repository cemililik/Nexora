namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Immutable record representing a single audit log entry.
/// </summary>
/// <param name="Id">Unique identifier for the audit entry.</param>
/// <param name="TenantId">The tenant that owns this audit entry.</param>
/// <param name="Module">The module where the operation occurred (e.g., "Contacts", "CRM").</param>
/// <param name="Operation">The operation name (e.g., "CreateContact", "UpdateLead").</param>
/// <param name="OperationType">The category of operation.</param>
/// <param name="UserId">The authenticated user who performed the operation, if available.</param>
/// <param name="UserEmail">The email of the authenticated user, if available.</param>
/// <param name="IpAddress">The client IP address.</param>
/// <param name="UserAgent">The client User-Agent header value.</param>
/// <param name="CorrelationId">The correlation identifier for distributed tracing.</param>
/// <param name="IsSuccess">Whether the operation completed successfully.</param>
/// <param name="ErrorKey">The localization key for the error, if the operation failed.</param>
/// <param name="EntityType">The type of entity affected (e.g., "Contact", "Lead").</param>
/// <param name="EntityId">The identifier of the entity affected.</param>
/// <param name="BeforeState">JSON representation of the entity state before the operation.</param>
/// <param name="AfterState">JSON representation of the entity state after the operation.</param>
/// <param name="Changes">JSON representation of the specific changes made.</param>
/// <param name="Metadata">Additional JSON metadata associated with the operation.</param>
/// <param name="Timestamp">When the operation occurred.</param>
public sealed record AuditEntry(
    Guid Id,
    string TenantId,
    string Module,
    string Operation,
    OperationType OperationType,
    Guid? UserId,
    string? UserEmail,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    bool IsSuccess,
    string? ErrorKey,
    string? EntityType,
    string? EntityId,
    string? BeforeState,
    string? AfterState,
    string? Changes,
    string? Metadata,
    DateTimeOffset Timestamp);
