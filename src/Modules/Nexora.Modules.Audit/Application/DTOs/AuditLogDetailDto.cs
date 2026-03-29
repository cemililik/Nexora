namespace Nexora.Modules.Audit.Application.DTOs;

/// <summary>Detail DTO for a single audit log entry with full data.</summary>
public sealed record AuditLogDetailDto(
    Guid Id,
    string Module,
    string Operation,
    string OperationType,
    string? UserEmail,
    bool IsSuccess,
    string? EntityType,
    string? EntityId,
    DateTimeOffset Timestamp,
    Guid? UserId,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    string? ErrorKey,
    string? BeforeState,
    string? AfterState,
    string? Changes,
    string? Metadata);
