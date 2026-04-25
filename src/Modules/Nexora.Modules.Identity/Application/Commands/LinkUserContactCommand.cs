using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Links a user to a Contacts module contact record.</summary>
public sealed record LinkUserContactCommand(Guid UserId, Guid ContactId) : ICommand;

/// <summary>Validates <see cref="LinkUserContactCommand"/> input.</summary>
public sealed class LinkUserContactValidator : AbstractValidator<LinkUserContactCommand>
{
    /// <summary>Initializes the validator.</summary>
    public LinkUserContactValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");

        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_identity_user_link_contact_contact_id_required");
    }
}

/// <summary>
/// Applies a user↔contact link, enqueues <see cref="UserContactLinkedIntegrationEvent"/> via the outbox,
/// and persists atomically.
/// </summary>
public sealed class LinkUserContactHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IOutbox outbox,
    ILogger<LinkUserContactHandler> logger) : ICommandHandler<LinkUserContactCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(LinkUserContactCommand request, CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantGuid)
        {
            logger.LogWarning("LinkUserContact rejected — invalid tenant context for user {UserId}", request.UserId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_invalid_tenant_context"));
        }

        var tenantId = TenantId.From(tenantGuid);
        var userId = UserId.From(request.UserId);

        if (!Guid.TryParse(tenantContextAccessor.Current.UserId, out var linkedByUserGuid)
            || linkedByUserGuid == Guid.Empty)
        {
            logger.LogWarning("User contact link rejected — no valid actor user context for user {UserId}", request.UserId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_invalid_user_context"));
        }

        var linkedByUserId = UserId.From(linkedByUserGuid);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("LinkContact: user {UserId} not found for tenant {TenantId}", request.UserId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_user_link_contact_user_not_found"));
        }

        if (user.IsSystemAccount)
        {
            logger.LogWarning("LinkContact rejected: user {UserId} is a system account", request.UserId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_user_link_contact_system_account_rejected"));
        }

        if (user.ContactId is not null)
        {
            logger.LogWarning("LinkContact rejected: user {UserId} already linked to contact {ContactId}",
                request.UserId, user.ContactId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_user_link_contact_already_linked"));
        }

        var alreadyLinked = await dbContext.Users
            .AnyAsync(u => u.ContactId == request.ContactId
                        && u.TenantId == tenantId
                        && u.Id != userId, cancellationToken);

        if (alreadyLinked)
        {
            logger.LogWarning(
                "LinkContact rejected: contact {ContactId} already linked to another user in tenant {TenantId}",
                request.ContactId, tenantId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_user_link_contact_contact_already_in_use"));
        }

        try
        {
            user.LinkContact(request.ContactId, linkedByUserId);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("LinkContact domain rejection for user {UserId}: {Key}", request.UserId, ex.LocalizationKey);
            return Result.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        var linkedAt = DateTime.UtcNow;

        await outbox.EnqueueAsync(new UserContactLinkedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            UserId = request.UserId,
            ContactId = request.ContactId,
            LinkedAtUtc = linkedAt,
            LinkedByUserId = linkedByUserGuid
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} linked to contact {ContactId} by {LinkedByUserId} in tenant {TenantId}",
            request.UserId, request.ContactId, linkedByUserGuid, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_user_link_contact_success"));
    }
}
