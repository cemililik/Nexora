using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to test a notification provider by sending a test message.</summary>
public sealed record TestNotificationProviderCommand(
    Guid Id,
    string TestAddress) : ICommand<object>;

/// <summary>Validates provider test input.</summary>
public sealed class TestNotificationProviderValidator : AbstractValidator<TestNotificationProviderCommand>
{
    public TestNotificationProviderValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("lockey_notifications_validation_provider_id_required");

        RuleFor(x => x.TestAddress)
            .NotEmpty().WithMessage("lockey_notifications_validation_test_address_required")
            .MaximumLength(256).WithMessage("lockey_notifications_validation_test_address_max_length");
    }
}

/// <summary>Tests a provider by validating its configuration. Actual delivery in Batch 4.</summary>
public sealed class TestNotificationProviderHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<TestNotificationProviderHandler> logger) : ICommandHandler<TestNotificationProviderCommand, object>
{
    public async Task<Result<object>> Handle(
        TestNotificationProviderCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var providerId = NotificationProviderId.From(request.Id);

        var provider = await dbContext.NotificationProviders
            .FirstOrDefaultAsync(p => p.Id == providerId && p.TenantId == tenantId, cancellationToken);

        if (provider is null)
        {
            logger.LogWarning("Provider {ProviderId} not found for test in tenant {TenantId}", request.Id, tenantId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_provider_not_found"));
        }

        if (!provider.IsActive)
        {
            logger.LogWarning("Provider {ProviderId} is inactive, cannot test in tenant {TenantId}", request.Id, tenantId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_provider_inactive"));
        }

        // Actual provider test (API call) will be implemented in Batch 4 with delivery jobs
        logger.LogInformation("Provider {ProviderId} ({ProviderName}) test initiated for address {TestAddress} in tenant {TenantId}",
            provider.Id, provider.ProviderName, request.TestAddress, tenantId);

        return Result<object>.Success(null!,
            LocalizedMessage.Of("lockey_notifications_provider_test_initiated"));
    }
}
