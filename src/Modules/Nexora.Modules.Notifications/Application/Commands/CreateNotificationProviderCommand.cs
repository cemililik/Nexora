using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to create a notification provider configuration.</summary>
public sealed record CreateNotificationProviderCommand(
    string Channel,
    string ProviderName,
    string Config,
    int DailyLimit,
    bool IsDefault = false) : ICommand<NotificationProviderDto>;

/// <summary>Validates provider creation input.</summary>
public sealed class CreateNotificationProviderValidator : AbstractValidator<CreateNotificationProviderCommand>
{
    public CreateNotificationProviderValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("lockey_notifications_validation_provider_channel_required")
            .Must(BeValidChannel).WithMessage("lockey_notifications_validation_provider_channel_invalid");

        RuleFor(x => x.ProviderName)
            .NotEmpty().WithMessage("lockey_notifications_validation_provider_name_required")
            .Must(BeValidProviderName).WithMessage("lockey_notifications_validation_provider_name_invalid");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("lockey_notifications_validation_provider_config_required");

        RuleFor(x => x.DailyLimit)
            .GreaterThan(0).WithMessage("lockey_notifications_validation_provider_daily_limit_positive");
    }

    private static bool BeValidChannel(string channel) =>
        Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out _);

    private static bool BeValidProviderName(string name) =>
        Enum.TryParse<ProviderName>(name, ignoreCase: true, out _);
}

/// <summary>Creates a notification provider and persists it to the database.</summary>
public sealed class CreateNotificationProviderHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateNotificationProviderHandler> logger) : ICommandHandler<CreateNotificationProviderCommand, NotificationProviderDto>
{
    public async Task<Result<NotificationProviderDto>> Handle(
        CreateNotificationProviderCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var channel = Enum.Parse<NotificationChannel>(request.Channel, ignoreCase: true);
        var providerName = Enum.Parse<ProviderName>(request.ProviderName, ignoreCase: true);

        var exists = await dbContext.NotificationProviders
            .AnyAsync(p => p.TenantId == tenantId && p.Channel == channel && p.ProviderName == providerName,
                cancellationToken);

        if (exists)
        {
            logger.LogWarning("Provider creation failed: {ProviderName} for {Channel} already exists in tenant {TenantId}",
                providerName, channel, tenantId);
            return Result<NotificationProviderDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_provider_already_exists",
                new Dictionary<string, string> { ["provider"] = request.ProviderName, ["channel"] = request.Channel }));
        }

        var provider = NotificationProvider.Create(tenantId, channel, providerName, request.Config, request.DailyLimit, request.IsDefault);
        await dbContext.NotificationProviders.AddAsync(provider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new NotificationProviderDto(
            provider.Id.Value, provider.Channel.ToString(), provider.ProviderName.ToString(),
            provider.IsDefault, provider.IsActive, provider.DailyLimit, provider.SentToday,
            provider.CreatedAt);

        logger.LogInformation("Provider {ProviderId} ({ProviderName}/{Channel}) created for tenant {TenantId}",
            provider.Id, providerName, channel, tenantId);

        return Result<NotificationProviderDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_provider_created"));
    }
}
