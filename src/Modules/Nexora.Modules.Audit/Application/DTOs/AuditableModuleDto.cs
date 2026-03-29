namespace Nexora.Modules.Audit.Application.DTOs;

/// <summary>Groups auditable operations by module name.</summary>
public sealed record AuditableModuleDto(string Module, List<AuditableOperationDto> Operations);
