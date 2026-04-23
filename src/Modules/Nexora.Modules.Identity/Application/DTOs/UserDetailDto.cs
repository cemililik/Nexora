namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>User detail with organization memberships and preferences.</summary>
public sealed record UserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Status,
    DateTimeOffset? LastLoginAt,
    List<UserOrganizationDto> Organizations,
    List<string>? Permissions = null,
    string? PreferredLanguage = null,
    Guid? ContactId = null,
    bool IsSystemAccount = false);

/// <summary>Organization membership info for a user.</summary>
public sealed record UserOrganizationDto(
    Guid OrganizationId,
    string OrganizationName,
    bool IsDefault);
