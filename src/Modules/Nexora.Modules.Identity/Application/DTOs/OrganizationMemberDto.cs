namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>Organization member info returned from member queries.</summary>
public sealed record OrganizationMemberDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    bool IsDefaultOrg);
