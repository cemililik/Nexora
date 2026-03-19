namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>Tenant summary returned from list queries.</summary>
public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string? RealmId,
    DateTimeOffset CreatedAt);

/// <summary>Tenant detail with installed modules, returned from get-by-id queries.</summary>
public sealed record TenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string? RealmId,
    string? Settings,
    DateTimeOffset CreatedAt,
    List<string> InstalledModules);
