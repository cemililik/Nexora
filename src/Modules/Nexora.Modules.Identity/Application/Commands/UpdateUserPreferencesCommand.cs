using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Constants;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to update the current user's locale preferences (UI language).</summary>
public sealed record UpdateUserPreferencesCommand(
    string KeycloakUserId,
    string? PreferredLanguage) : ICommand;

/// <summary>Validates user preferences input: language must be supported when provided.</summary>
public sealed class UpdateUserPreferencesValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesValidator()
    {
        RuleFor(x => x.KeycloakUserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");

        When(x => !string.IsNullOrWhiteSpace(x.PreferredLanguage), () =>
        {
            RuleFor(x => x.PreferredLanguage!)
                .Must(lang => LocaleConstants.SupportedLanguages.Contains(lang.Trim().ToLowerInvariant()))
                .WithMessage("lockey_identity_validation_language_unsupported");
        });
    }
}

/// <summary>Persists updated locale preferences for the current user.</summary>
public sealed class UpdateUserPreferencesHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateUserPreferencesHandler> logger) : ICommandHandler<UpdateUserPreferencesCommand>
{
    /// <summary>Persists updated locale preferences for the current user.</summary>
    public async Task<Result> Handle(
        UpdateUserPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(
                u => u.KeycloakUserId == request.KeycloakUserId && u.TenantId == tenantId,
                cancellationToken);

        if (user is null)
        {
            logger.LogWarning("User preferences update failed: user not found for tenant {TenantId}", tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_user_not_found"));
        }

        user.UpdatePreferences(request.PreferredLanguage?.Trim().ToLowerInvariant());
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} preferences updated: PreferredLanguage={Language}",
            user.Id, request.PreferredLanguage ?? "(cleared)");

        return Result.Success(LocalizedMessage.Of("lockey_identity_user_preferences_updated"));
    }
}
