using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Removes the link between a user and a Contacts module contact record.</summary>
public sealed record UnlinkUserContactCommand(Guid UserId) : ICommand;

/// <summary>Validates <see cref="UnlinkUserContactCommand"/> input.</summary>
public sealed class UnlinkUserContactValidator : AbstractValidator<UnlinkUserContactCommand>
{
    /// <summary>Initializes the validator.</summary>
    public UnlinkUserContactValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");
    }
}

/// <summary>
/// Removes a user↔contact link, enqueues <see cref="UserContactUnlinkedIntegrationEvent"/> via the outbox,
/// and persists atomically. Idempotent — a no-op when the user is already unlinked.
/// </summary>
public sealed class UnlinkUserContactHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IOutbox outbox,
    ILogger<UnlinkUserContactHandler> logger) : ICommandHandler<UnlinkUserContactCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(UnlinkUserContactCommand request, CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantGuid)
        {
            logger.LogWarning("UnlinkUserContact rejected — invalid tenant context for user {UserId}", request.UserId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_invalid_tenant_context"));
        }

        var tenantId = TenantId.From(tenantGuid);
        var userId = UserId.From(request.UserId);

        if (!Guid.TryParse(tenantContextAccessor.Current.UserId, out var unlinkedByUserGuid)
            || unlinkedByUserGuid == Guid.Empty)
        {
            logger.LogWarning("UnlinkUserContact rejected — no valid actor user context for user {UserId}", request.UserId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_invalid_user_context"));
        }

        var unlinkedByUserId = UserId.From(unlinkedByUserGuid);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("UnlinkContact: user {UserId} not found for tenant {TenantId}", request.UserId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_user_link_contact_user_not_found"));
        }

        if (user.ContactId is null)
        {
            // Idempotent — no-op and success.
            logger.LogInformation("UnlinkContact no-op: user {UserId} has no contact link", request.UserId);
            return Result.Success(LocalizedMessage.Of("lockey_identity_user_link_contact_unlink_success"));
        }

        // Capture the previously linked ContactId before clearing it so downstream consumers
        // can correlate the unlink with the contact record.
        var previousContactId = user.ContactId;

        user.UnlinkContact(unlinkedByUserId);

        var unlinkedAt = DateTime.UtcNow;

        await outbox.EnqueueAsync(new UserContactUnlinkedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            UserId = request.UserId,
            ContactId = previousContactId,
            UnlinkedAtUtc = unlinkedAt,
            Reason = "manual"
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} unlinked from contact in tenant {TenantId} (reason=manual)",
            request.UserId, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_user_link_contact_unlink_success"));
    }
}
