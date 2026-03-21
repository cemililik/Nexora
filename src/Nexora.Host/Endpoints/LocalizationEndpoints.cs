using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Host.Endpoints;

/// <summary>
/// API endpoints for retrieving localized translations.
/// Used by frontend clients to resolve lockey_ keys to translated strings.
/// </summary>
public static class LocalizationEndpoints
{
    /// <summary>Maps localization endpoints under /api/v1/localization.</summary>
    public static void MapLocalizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/localization")
            .AllowAnonymous();

        group.MapGet("/{languageCode}", GetTranslationsAsync);
        group.MapGet("/{languageCode}/{key}", GetSingleTranslationAsync);
    }

    private static async Task<IResult> GetTranslationsAsync(
        string languageCode,
        string? module,
        ILocalizationService localizationService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var tenantId = TryGetTenantId(httpContext);

        Dictionary<string, string> translations;

        if (!string.IsNullOrWhiteSpace(module))
        {
            translations = await localizationService.GetByModuleAsync(
                module, languageCode, tenantId, ct);
        }
        else
        {
            translations = await localizationService.GetAllAsync(
                languageCode, tenantId, ct);
        }

        return Results.Ok(ApiEnvelope<Dictionary<string, string>>.Success(translations));
    }

    private static async Task<IResult> GetSingleTranslationAsync(
        string languageCode,
        string key,
        ILocalizationService localizationService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var tenantId = TryGetTenantId(httpContext);

        var value = await localizationService.GetAsync(key, languageCode, tenantId, ct);

        if (value is null)
            return Results.NotFound(
                ApiEnvelope<string>.Fail(new Error(
                    Nexora.SharedKernel.Localization.LocalizedMessage.Of("lockey_localization_key_not_found",
                        new Dictionary<string, string> { ["key"] = key }))));

        return Results.Ok(ApiEnvelope<Dictionary<string, string>>.Success(
            new Dictionary<string, string> { [key] = value }));
    }

    private static Guid? TryGetTenantId(HttpContext httpContext)
    {
        var tenantClaim = httpContext.User.FindFirst("tenant_id")?.Value;
        return tenantClaim is not null && Guid.TryParse(tenantClaim, out var tenantId)
            ? tenantId
            : null;
    }
}
