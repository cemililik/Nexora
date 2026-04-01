namespace Nexora.Modules.Audit.Application.Commands;

/// <summary>Single setting item within a bulk update request.</summary>
public sealed record AuditSettingItem(string Module, string Operation, bool IsEnabled, int RetentionDays);
