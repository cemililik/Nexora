using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>Minimal API endpoints for user management within a tenant.</summary>
public static class UserEndpoints
{
    /// <summary>Maps user CRUD, profile, status, and /me endpoints.</summary>
    public static void MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, Guid? organizationId, Guid? roleId, string? search, ISender sender, CancellationToken ct) =>
        {
            var query = new GetUsersQuery(page ?? 1, pageSize ?? 20, organizationId, roleId, search);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<UserDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<UserDto>>.Fail(result.Error!));
        });

        group.MapGet("/me", async (HttpContext httpContext, ISender sender, IdentityDbContext dbContext, CancellationToken ct) =>
        {
            var keycloakUserId = httpContext.User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(keycloakUserId))
                return Results.Unauthorized();

            var result = await sender.Send(new GetCurrentUserQuery(keycloakUserId), ct);
            if (result.IsSuccess)
            {
                // SAFE: ExecuteUpdateAsync filters by strongly-typed UserId — no tenant isolation bypass risk.
                // Awaited to avoid connection pool corruption.
                await dbContext.Users
                    .Where(u => u.Id == Domain.ValueObjects.UserId.From(result.Value!.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, DateTimeOffset.UtcNow), ct);
            }

            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<UserDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<UserDetailDto>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<UserDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<UserDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateUserCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/users/{result.Value!.Id}",
                    ApiEnvelope<UserDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<UserDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}/profile", async (Guid id, UpdateProfileRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateUserProfileCommand(id, request.FirstName, request.LastName, request.Phone);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<UserDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<UserDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}/status", async (Guid id, UpdateUserStatusRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateUserStatusCommand(id, request.Action);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteUserCommand(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}/roles", async (Guid id, Guid? organizationId, ISender sender, CancellationToken ct) =>
        {
            if (organizationId is null)
                return Results.BadRequest(ApiEnvelope<List<RoleDto>>.Fail(
                    new Error(LocalizedMessage.Of("lockey_validation_required", new() { ["field"] = "organizationId" }))));

            var result = await sender.Send(new GetUserRolesQuery(id, organizationId.Value), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<List<RoleDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<List<RoleDto>>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}/roles", async (Guid id, AssignRolesRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AssignUserRolesCommand(id, request.OrganizationId, request.RoleIds);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for updating a user's profile.</summary>
public sealed record UpdateProfileRequest(string FirstName, string LastName, string? Phone);

/// <summary>Request body for changing a user's status.</summary>
public sealed record UpdateUserStatusRequest(string Action);

/// <summary>Request body for assigning roles to a user within an organization.</summary>
public sealed record AssignRolesRequest(Guid OrganizationId, List<Guid> RoleIds);
