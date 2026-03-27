using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>Minimal API endpoints for tenant module management.</summary>
public static class ModuleEndpoints
{
    /// <summary>Maps module install, uninstall, and listing endpoints.</summary>
    public static void MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/modules")
            .RequireAuthorization();

        group.MapGet("/", async (Guid tenantId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTenantModulesQuery(tenantId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<List<TenantModuleDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<List<TenantModuleDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid tenantId, InstallModuleRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new InstallModuleCommand(tenantId, request.ModuleName);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/tenants/{tenantId}/modules",
                    ApiEnvelope<TenantModuleDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<TenantModuleDto>.Fail(result.Error!));
        });

        group.MapPatch("/{moduleName}/activate", async (Guid tenantId, string moduleName, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ActivateModuleCommand(tenantId, moduleName), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapPatch("/{moduleName}/deactivate", async (Guid tenantId, string moduleName, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeactivateModuleCommand(tenantId, moduleName), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapDelete("/{moduleName}", async (Guid tenantId, string moduleName, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UninstallModuleCommand(tenantId, moduleName), ct);

            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_identity_error_module_not_installed" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_identity_error_tenant_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for installing a module.</summary>
public sealed record InstallModuleRequest(string ModuleName);
