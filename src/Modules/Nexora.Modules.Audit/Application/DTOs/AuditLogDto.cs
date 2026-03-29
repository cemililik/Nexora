namespace Nexora.Modules.Audit.Application.DTOs;

/// <summary>Summary DTO for audit log list views.</summary>
public sealed record AuditLogDto(
    Guid Id,
    string Module,
    string Operation,
    string OperationType,
    string? UserEmail,
    bool IsSuccess,
    string? EntityType,
    string? EntityId,
    DateTimeOffset Timestamp);
