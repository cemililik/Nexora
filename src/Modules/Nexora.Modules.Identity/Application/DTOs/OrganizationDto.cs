namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>Data transfer object representing an organization summary.</summary>
public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string Timezone,
    string DefaultCurrency,
    string DefaultLanguage,
    bool IsActive);
