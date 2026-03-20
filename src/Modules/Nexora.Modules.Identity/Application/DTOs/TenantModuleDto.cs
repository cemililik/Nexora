namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>Tenant module installation info returned from queries.</summary>
public sealed record TenantModuleDto(
    Guid Id,
    string ModuleName,
    bool IsActive,
    DateTimeOffset InstalledAt,
    string? InstalledBy);
