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

/// <summary>Command to provision a new tenant with schema and default modules.</summary>
public sealed record CreateTenantCommand(
    string Name,
    string Slug) : ICommand<TenantDto>;

/// <summary>Validates tenant creation input (name, slug format and length).</summary>
public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_name_required")
            .MaximumLength(200).WithMessage("lockey_identity_validation_tenant_name_max_length");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_slug_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_tenant_slug_max_length")
            .Matches("^[a-z0-9-]+$").WithMessage("lockey_identity_validation_tenant_slug_format");
    }
}

/// <summary>Creates a tenant, provisions its schema, installs identity module, and activates.</summary>
public sealed class CreateTenantHandler(
    PlatformDbContext platformDb,
    ITenantSchemaManager schemaManager) : ICommandHandler<CreateTenantCommand, TenantDto>
{
    public async Task<Result<TenantDto>> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        // Check slug uniqueness
        var slugExists = await platformDb.Tenants
            .AnyAsync(t => t.Slug == request.Slug.ToLowerInvariant(), cancellationToken);

        if (slugExists)
        {
            return Result<TenantDto>.Failure(
                "lockey_identity_error_tenant_slug_taken",
                new Dictionary<string, string> { ["slug"] = request.Slug });
        }

        // Create tenant entity
        var tenant = Tenant.Create(request.Name, request.Slug);

        await platformDb.Tenants.AddAsync(tenant, cancellationToken);
        await platformDb.SaveChangesAsync(cancellationToken);

        // Provision tenant schema + run migrations
        var schemaName = $"tenant_{tenant.Id.Value}";
        await schemaManager.CreateSchemaAsync(schemaName, cancellationToken);

        // Install identity module by default (core module)
        var identityModule = TenantModule.Create(tenant.Id, "identity");
        await platformDb.TenantModules.AddAsync(identityModule, cancellationToken);
        await platformDb.SaveChangesAsync(cancellationToken);

        // Activate tenant after successful provisioning
        tenant.Activate();
        await platformDb.SaveChangesAsync(cancellationToken);

        var dto = new TenantDto(
            tenant.Id.Value,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.RealmId,
            DateTimeOffset.UtcNow);

        return Result<TenantDto>.Success(dto,
            new LocalizedMessage("lockey_identity_tenant_created"));
    }
}
