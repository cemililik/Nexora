using System.Data.Common;
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

/// <summary>Creates a tenant, provisions Keycloak realm, schema, installs identity module, and activates.</summary>
public sealed class CreateTenantHandler(
    PlatformDbContext platformDb,
    ITenantSchemaManager schemaManager,
    IKeycloakAdminService keycloakAdmin,
    ILogger<CreateTenantHandler> logger) : ICommandHandler<CreateTenantCommand, TenantDto>
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
            logger.LogWarning("Tenant creation failed: slug {Slug} already taken", request.Slug);
            return Result<TenantDto>.Failure(
                LocalizedMessage.Of("lockey_identity_error_tenant_slug_taken",
                new Dictionary<string, string> { ["slug"] = request.Slug }));
        }

        // CONSISTENCY: Tier 2B — External-First + Compensation.
        // Keycloak realm must be created first because CreateRealmAsync returns the realm name
        // we store. The realm name is deterministic (tenant-{slug}), so if the subsequent DB
        // write fails we compensate by deleting the realm. CreateRealmAsync is idempotent —
        // a 409 Conflict means the realm already exists and is treated as success.
        var realmName = $"tenant-{request.Slug.ToLowerInvariant()}";
        var createdRealmId = await keycloakAdmin.CreateRealmAsync(realmName, request.Name, cancellationToken);

        // Provision tenant schema (idempotent — CREATE SCHEMA IF NOT EXISTS)
        var tenant = Tenant.Create(request.Name, request.Slug);
        var schemaName = $"tenant_{tenant.Id.Value}";

        try
        {
            await schemaManager.CreateSchemaAsync(schemaName, cancellationToken);
        }
        catch (DbException schemaEx)
        {
            logger.LogError(schemaEx,
                "Schema creation failed for tenant {TenantId}; compensating Keycloak realm {RealmName}",
                tenant.Id, realmName);

            try { await keycloakAdmin.DeleteRealmAsync(realmName, cancellationToken); }
            catch (HttpRequestException compEx)
            {
                logger.LogCritical(compEx,
                    "COMPENSATION FAILED: Keycloak realm {RealmName} is orphaned. Manual cleanup required.",
                    realmName);
            }

            return Result<TenantDto>.Failure(LocalizedMessage.Of("lockey_identity_error_tenant_create_failed"));
        }

        // Persist tenant + realm ID + identity module + active status — single commit
        tenant.SetRealmId(createdRealmId);
        var identityModule = TenantModule.Create(tenant.Id, "identity");
        tenant.Activate();

        await platformDb.Tenants.AddAsync(tenant, cancellationToken);
        await platformDb.TenantModules.AddAsync(identityModule, cancellationToken);

        try
        {
            await platformDb.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx,
                "DB write failed after provisioning tenant {TenantId}; compensating realm {RealmName} and schema {SchemaName}",
                tenant.Id, realmName, schemaName);

            try { await keycloakAdmin.DeleteRealmAsync(realmName, cancellationToken); }
            catch (HttpRequestException compEx)
            {
                logger.LogCritical(compEx,
                    "COMPENSATION FAILED: Keycloak realm {RealmName} is orphaned. Manual cleanup required.",
                    realmName);
            }

            try { await schemaManager.DropSchemaAsync(schemaName, cancellationToken); }
            catch (DbException schemaCompEx)
            {
                logger.LogCritical(schemaCompEx,
                    "COMPENSATION FAILED: tenant schema {SchemaName} is orphaned for tenant {TenantId}. Manual cleanup required.",
                    schemaName, tenant.Id);
            }

            return Result<TenantDto>.Failure(LocalizedMessage.Of("lockey_identity_error_tenant_create_failed"));
        }

        var dto = new TenantDto(
            tenant.Id.Value,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.RealmId,
            DateTimeOffset.UtcNow);

        logger.LogInformation("Tenant {TenantId} created with slug {Slug}", tenant.Id, tenant.Slug);

        return Result<TenantDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_tenant_created"));
    }
}
