namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>User detail with organization memberships.</summary>
public sealed record UserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Status,
    DateTimeOffset? LastLoginAt,
    List<UserOrganizationDto> Organizations,
    List<string>? Permissions = null);

/// <summary>Organization membership info for a user.</summary>
public sealed record UserOrganizationDto(
    Guid OrganizationId,
    string OrganizationName,
    bool IsDefault);
