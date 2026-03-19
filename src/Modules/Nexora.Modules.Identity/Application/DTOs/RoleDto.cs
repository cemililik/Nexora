namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>
/// Role summary returned from queries.
/// </summary>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    List<string> Permissions);
