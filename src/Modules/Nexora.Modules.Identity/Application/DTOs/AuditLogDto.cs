namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>Audit log entry returned from queries.</summary>
public sealed record AuditLogDto(
    Guid Id,
    Guid UserId,
    string Action,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset Timestamp,
    string? Details);
