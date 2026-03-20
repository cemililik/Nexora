using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to create a user within the current tenant. Provisions user in Keycloak automatically.</summary>
public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string TemporaryPassword) : ICommand<UserDto>;

/// <summary>Validates user creation input (email, names, temporary password).</summary>
public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("lockey_identity_validation_email_required")
            .EmailAddress().WithMessage("lockey_identity_validation_email_format");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("lockey_identity_validation_first_name_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_first_name_max_length");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("lockey_identity_validation_last_name_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_last_name_max_length");

        RuleFor(x => x.TemporaryPassword)
            .NotEmpty().WithMessage("lockey_identity_validation_password_required")
            .MinimumLength(8).WithMessage("lockey_identity_validation_password_min_length");
    }
}

/// <summary>Creates a user in Keycloak and the local database after verifying email uniqueness.</summary>
public sealed class CreateUserHandler(
    IdentityDbContext dbContext,
    PlatformDbContext platformDb,
    ITenantContextAccessor tenantContextAccessor,
    IKeycloakAdminService keycloakAdmin,
    ILogger<CreateUserHandler> logger) : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var emailExists = await dbContext.Users
            .AnyAsync(u => u.TenantId == tenantId && u.Email == request.Email.ToLowerInvariant(),
                cancellationToken);

        if (emailExists)
        {
            logger.LogWarning("User creation failed: email {Email} already taken for tenant {TenantId}", request.Email, tenantId);
            return Result<UserDto>.Failure(
                "lockey_identity_error_user_email_taken",
                new Dictionary<string, string> { ["email"] = request.Email });
        }

        // Resolve tenant's Keycloak realm
        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant?.RealmId is null)
        {
            logger.LogWarning("User creation failed: tenant {TenantId} realm not configured", tenantId);
            return Result<UserDto>.Failure("lockey_identity_error_tenant_realm_not_configured");
        }

        // Create user in Keycloak
        var keycloakUserId = await keycloakAdmin.CreateUserAsync(
            tenant.RealmId,
            request.Email,
            request.Email,
            request.FirstName,
            request.LastName,
            request.TemporaryPassword,
            cancellationToken);

        var user = User.Create(tenantId, keycloakUserId, request.Email,
            request.FirstName, request.LastName);

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new UserDto(
            user.Id.Value,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Phone,
            user.Status.ToString(),
            user.LastLoginAt);

        logger.LogInformation("User {UserId} created with email {Email} for tenant {TenantId}", user.Id, user.Email, tenantId);

        return Result<UserDto>.Success(dto,
            new LocalizedMessage("lockey_identity_user_created"));
    }
}
