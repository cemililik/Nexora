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

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRoleByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<RoleDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<RoleDetailDto>.Fail(result.Error!));
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

        group.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateRoleCommand(id, request.Name, request.Description, request.PermissionIds);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<RoleDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<RoleDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteRoleCommand(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}/users", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRoleUsersQuery(id, page ?? 1, pageSize ?? 20), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<RoleUserDto>>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<PagedResult<RoleUserDto>>.Fail(result.Error!));
        });

        group.MapPost("/{id:guid}/users", async (Guid id, AddUserToRoleRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddUserToRoleCommand(id, request.UserId);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}/users/{userId:guid}", async (Guid id, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var command = new RemoveUserFromRoleCommand(id, userId);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
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

public sealed record UpdateRoleRequest(string Name, string? Description, List<Guid>? PermissionIds);

/// <summary>Request body for adding a user to a role.</summary>
public sealed record AddUserToRoleRequest(Guid UserId);
