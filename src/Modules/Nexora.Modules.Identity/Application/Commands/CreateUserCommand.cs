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
                LocalizedMessage.Of("lockey_identity_error_email_already_exists",
                new Dictionary<string, string> { ["email"] = request.Email }));
        }

        // Resolve tenant's Keycloak realm
        var tenant = await platformDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant?.RealmId is null)
        {
            logger.LogWarning("User creation failed: tenant {TenantId} realm not configured", tenantId);
            return Result<UserDto>.Failure(LocalizedMessage.Of("lockey_identity_error_tenant_realm_not_configured"));
        }

        // CONSISTENCY: Tier 2B — External-First + Compensation.
        // Keycloak must be called first because it returns the keycloakUserId we store.
        // If the subsequent DB write fails we compensate by deleting the Keycloak user.
        string keycloakUserId;
        try
        {
            keycloakUserId = await keycloakAdmin.CreateUserAsync(
                tenant.RealmId,
                request.Email,
                request.Email,
                request.FirstName,
                request.LastName,
                request.TemporaryPassword,
                cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            logger.LogWarning(
                "User creation rejected: email already exists in realm {Realm} for tenant {TenantId}",
                tenant.RealmId, tenantId);
            return Result<UserDto>.Failure(LocalizedMessage.Of("lockey_identity_error_email_already_exists"));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Keycloak user creation failed in realm {Realm} for tenant {TenantId}",
                tenant.RealmId, tenantId);
            return Result<UserDto>.Failure(LocalizedMessage.Of("lockey_identity_error_user_create_failed"));
        }

        var user = User.Create(tenantId, keycloakUserId, request.Email,
            request.FirstName, request.LastName);

        try
        {
            await dbContext.Users.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx,
                "DB write failed after Keycloak user creation in realm {Realm} for tenant {TenantId}; compensating",
                tenant.RealmId, tenantId);

            // Compensation must not inherit the caller's cancellation token — if the original
            // request was cancelled, compensation would abort mid-flight and orphan the Keycloak user.
            using var compensationCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await keycloakAdmin.DeleteUserAsync(tenant.RealmId, keycloakUserId, compensationCts.Token);
                logger.LogInformation(
                    "Compensation succeeded: Keycloak user {KeycloakUserId} removed from realm {Realm}",
                    keycloakUserId, tenant.RealmId);
            }
            catch (HttpRequestException compEx)
            {
                logger.LogCritical(compEx,
                    "COMPENSATION FAILED: Keycloak user {KeycloakUserId} in realm {Realm} is orphaned. Manual cleanup required.",
                    keycloakUserId, tenant.RealmId);
            }
            catch (OperationCanceledException compEx)
            {
                logger.LogCritical(compEx,
                    "COMPENSATION TIMED OUT: Keycloak user {KeycloakUserId} in realm {Realm} may be orphaned. Manual cleanup required.",
                    keycloakUserId, tenant.RealmId);
            }

            return Result<UserDto>.Failure(LocalizedMessage.Of("lockey_identity_error_user_create_failed"));
        }

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
            LocalizedMessage.Of("lockey_identity_user_created"));
    }
}
