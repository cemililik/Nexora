using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to update a notification provider's configuration.</summary>
public sealed record UpdateNotificationProviderCommand(
    Guid Id,
    string Config,
    int DailyLimit,
    bool IsDefault) : ICommand<NotificationProviderDto>;

/// <summary>Validates provider update input.</summary>
public sealed class UpdateNotificationProviderValidator : AbstractValidator<UpdateNotificationProviderCommand>
{
    public UpdateNotificationProviderValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("lockey_notifications_validation_provider_id_required");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("lockey_notifications_validation_provider_config_required");

        RuleFor(x => x.DailyLimit)
            .GreaterThan(0).WithMessage("lockey_notifications_validation_provider_daily_limit_positive");
    }
}

/// <summary>Updates a notification provider's config, daily limit, and default status.</summary>
public sealed class UpdateNotificationProviderHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateNotificationProviderHandler> logger) : ICommandHandler<UpdateNotificationProviderCommand, NotificationProviderDto>
{
    public async Task<Result<NotificationProviderDto>> Handle(
        UpdateNotificationProviderCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var providerId = NotificationProviderId.From(request.Id);

        var provider = await dbContext.NotificationProviders
            .FirstOrDefaultAsync(p => p.Id == providerId && p.TenantId == tenantId, cancellationToken);

        if (provider is null)
        {
            logger.LogWarning("Provider {ProviderId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result<NotificationProviderDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_provider_not_found"));
        }

        provider.Update(request.Config, request.DailyLimit, request.IsDefault);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new NotificationProviderDto(
            provider.Id.Value, provider.Channel.ToString(), provider.ProviderName.ToString(),
            provider.IsDefault, provider.IsActive, provider.DailyLimit, provider.SentToday,
            provider.CreatedAt);

        logger.LogInformation("Provider {ProviderId} updated for tenant {TenantId}", provider.Id, tenantId);

        return Result<NotificationProviderDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_provider_updated"));
    }
}
