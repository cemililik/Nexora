using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Constants;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to update a tenant's locale settings (locale, currency, timezone, document language).</summary>
public sealed record UpdateTenantSettingsCommand(
    Guid TenantId,
    string DefaultLocale,
    string DefaultCurrency,
    string DefaultTimezone,
    string DefaultDocumentLanguage) : ICommand;

/// <summary>Validates tenant locale settings input against supported values.</summary>
public sealed class UpdateTenantSettingsValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_id_required");

        RuleFor(x => x.DefaultLocale)
            .NotEmpty().WithMessage("lockey_identity_validation_locale_required")
            .Must(LocaleConstants.SupportedLocales.Contains)
            .WithMessage("lockey_identity_validation_locale_unsupported");

        RuleFor(x => x.DefaultCurrency)
            .NotEmpty().WithMessage("lockey_identity_validation_currency_required")
            .Must(LocaleConstants.SupportedCurrencies.Contains)
            .WithMessage("lockey_identity_validation_currency_unsupported");

        RuleFor(x => x.DefaultTimezone)
            .NotEmpty().WithMessage("lockey_identity_validation_timezone_required")
            .Must(LocaleConstants.SupportedTimezones.Contains)
            .WithMessage("lockey_identity_validation_timezone_unsupported");

        RuleFor(x => x.DefaultDocumentLanguage)
            .NotEmpty().WithMessage("lockey_identity_validation_document_language_required")
            .Must(LocaleConstants.SupportedLanguages.Contains)
            .WithMessage("lockey_identity_validation_document_language_unsupported");
    }
}

/// <summary>Persists updated locale settings for the specified tenant.</summary>
public sealed class UpdateTenantSettingsHandler(
    PlatformDbContext platformDb,
    ILogger<UpdateTenantSettingsHandler> logger) : ICommandHandler<UpdateTenantSettingsCommand>
{
    public async Task<Result> Handle(
        UpdateTenantSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            logger.LogWarning("Tenant settings update failed: tenant {TenantId} not found", request.TenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_tenant_not_found"));
        }

        var settings = new TenantSettings(
            request.DefaultLocale,
            request.DefaultCurrency,
            request.DefaultTimezone,
            request.DefaultDocumentLanguage);

        tenant.UpdateSettings(settings);
        await platformDb.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Tenant {TenantId} locale settings updated: Locale={Locale}, Currency={Currency}, Timezone={Timezone}",
            request.TenantId, request.DefaultLocale, request.DefaultCurrency, request.DefaultTimezone);

        return Result.Success(LocalizedMessage.Of("lockey_identity_tenant_settings_updated"));
    }
}
