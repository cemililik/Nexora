namespace Nexora.Modules.Audit.Application.DTOs;

/// <summary>DTO for audit setting configuration.</summary>
public sealed record AuditSettingDto(
    Guid Id,
    string Module,
    string Operation,
    bool IsEnabled,
    int RetentionDays);
