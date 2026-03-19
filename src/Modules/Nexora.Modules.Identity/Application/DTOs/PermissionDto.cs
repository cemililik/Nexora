namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>
/// Permission detail returned from queries.
/// </summary>
public sealed record PermissionDto(
    Guid Id,
    string Module,
    string Resource,
    string Action,
    string Key,
    string? Description);
