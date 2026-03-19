namespace Nexora.Modules.Identity.Application.DTOs;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string Timezone,
    string DefaultCurrency,
    string DefaultLanguage,
    bool IsActive);
