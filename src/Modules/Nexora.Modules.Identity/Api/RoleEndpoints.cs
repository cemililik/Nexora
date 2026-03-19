using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>
/// Minimal API endpoints for role and permission management.
/// </summary>
public static class RoleEndpoints
{
    /// <summary>Maps role CRUD and permission listing endpoints.</summary>
    public static void MapRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/roles")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRolesQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<List<RoleDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<List<RoleDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateRoleCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/roles/{result.Value!.Id}",
                    ApiEnvelope<RoleDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<RoleDto>.Fail(result.Error!));
        });

        // Permissions listing
        endpoints.MapGroup("/permissions")
            .RequireAuthorization()
            .MapGet("/", async (string? module, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetPermissionsQuery(module), ct);
                return result.IsSuccess
                    ? Results.Ok(ApiEnvelope<List<PermissionDto>>.Success(result.Value!, result.Message))
                    : Results.BadRequest(ApiEnvelope<List<PermissionDto>>.Fail(result.Error!));
            });
    }
}
