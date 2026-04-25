using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>Minimal API endpoints for tenant module management.</summary>
public static class ModuleEndpoints
{
    /// <summary>Maps module install, uninstall, and listing endpoints.</summary>
    public static void MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Platform-level: list all registered (discoverable) modules
        endpoints.MapGet("/modules/registered", (IReadOnlyList<IModule> modules) =>
        {
            var result = modules.Select(m => new RegisteredModuleDto(
                m.Name, m.DisplayName, m.Version, m.Dependencies.ToList())).ToList();
            return Results.Ok(ApiEnvelope<List<RegisteredModuleDto>>.Success(result));
        }).RequireAuthorization("identity.modules.manage");

        // CR-01: tenant ID is sourced from ITenantContextAccessor (JWT claim via TenantMiddleware),
        // not from the URL route parameter. This prevents a caller from targeting another tenant
        // by crafting a different tenantId in the URL path.
        var group = endpoints.MapGroup("/tenants/modules")
            .RequireAuthorization("identity.modules.manage");

        static Guid GetTenantId(ITenantContextAccessor accessor) =>
            TenantId.Parse(accessor.Current.TenantId).Value;

        group.MapGet("/", async (ITenantContextAccessor tenantAccessor, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTenantModulesQuery(GetTenantId(tenantAccessor)), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<List<TenantModuleDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<List<TenantModuleDto>>.Fail(result.Error!));
        });

        // All four module-mutation endpoints (install / activate /
        // deactivate / uninstall) gate on the SAME permission as the
        // GET endpoints above (line 27, 33). Per review #42 the SPEC
        // entry for DELETE was inconsistent (claimed
        // `identity.modules.uninstall`, install row claimed
        // `platform.modules.manage`); the actual production posture is
        // ONE permission key — `identity.modules.manage` — for the
        // whole module-management resource. Adding RequireAuthorization
        // here brings the mutation endpoints into line with the read
        // endpoints AND closes a pre-existing gap where install /
        // uninstall / activate / deactivate were unauthenticated.
        group.MapPost("/", async (ITenantContextAccessor tenantAccessor, InstallModuleRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new InstallModuleCommand(GetTenantId(tenantAccessor), request.ModuleName);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/tenants/modules/{result.Value!.ModuleName}",
                    ApiEnvelope<TenantModuleDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<TenantModuleDto>.Fail(result.Error!));
        }).RequireAuthorization("identity.modules.manage");

        group.MapPatch("/{moduleName}/activate", async (ITenantContextAccessor tenantAccessor, string moduleName, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ActivateModuleCommand(GetTenantId(tenantAccessor), moduleName), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        }).RequireAuthorization("identity.modules.manage");

        group.MapPatch("/{moduleName}/deactivate", async (ITenantContextAccessor tenantAccessor, string moduleName, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeactivateModuleCommand(GetTenantId(tenantAccessor), moduleName), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        }).RequireAuthorization("identity.modules.manage");

        group.MapDelete("/{moduleName}", async (ITenantContextAccessor tenantAccessor, string moduleName, ISender sender, CancellationToken ct) =>
        {
            var tenantId = GetTenantId(tenantAccessor);
            var result = await sender.Send(new UninstallModuleCommand(tenantId, moduleName), ct);

            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_identity_error_module_not_installed" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_identity_error_tenant_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        }).RequireAuthorization("identity.modules.manage");
    }
}

/// <summary>Request body for installing a module.</summary>
public sealed record InstallModuleRequest(string ModuleName);

/// <summary>DTO for a registered (discoverable) module.</summary>
public sealed record RegisteredModuleDto(string Name, string DisplayName, string Version, List<string> Dependencies);
