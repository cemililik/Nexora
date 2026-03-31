namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>User assigned to a role with assignment context.</summary>
public sealed record RoleUserDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset AssignedAt);
