using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to create a user within the current tenant.</summary>
public sealed record CreateUserCommand(
    string KeycloakUserId,
    string Email,
    string FirstName,
    string LastName) : ICommand<UserDto>;

/// <summary>Validates user creation input (email, names, Keycloak ID).</summary>
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

        RuleFor(x => x.KeycloakUserId)
            .NotEmpty().WithMessage("lockey_identity_validation_keycloak_id_required");
    }
}

/// <summary>Creates a user after verifying email uniqueness within the tenant.</summary>
public sealed class CreateUserHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : ICommandHandler<CreateUserCommand, UserDto>
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
            return Result<UserDto>.Failure(
                "lockey_identity_error_user_email_taken",
                new Dictionary<string, string> { ["email"] = request.Email });
        }

        var user = User.Create(tenantId, request.KeycloakUserId, request.Email,
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

        return Result<UserDto>.Success(dto,
            new LocalizedMessage("lockey_identity_user_created"));
    }
}
