namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>Organization detail with member count, returned from get-by-id queries.</summary>
public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string Timezone,
    string DefaultCurrency,
    string DefaultLanguage,
    bool IsActive,
    int MemberCount);
