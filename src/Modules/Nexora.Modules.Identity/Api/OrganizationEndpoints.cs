using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>Minimal API endpoints for organization management within a tenant.</summary>
public static class OrganizationEndpoints
{
    /// <summary>Maps organization CRUD and member management endpoints.</summary>
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/organizations")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var query = new GetOrganizationsQuery(page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<OrganizationDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<OrganizationDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetOrganizationByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<OrganizationDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<OrganizationDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateOrganizationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/organizations/{result.Value!.Id}",
                    ApiEnvelope<OrganizationDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<OrganizationDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateOrganizationRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateOrganizationCommand(id, request.Name, request.Timezone,
                request.DefaultCurrency, request.DefaultLanguage);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<OrganizationDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<OrganizationDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteOrganizationCommand(id), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });

        // Member management
        group.MapGet("/{id:guid}/members", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var query = new GetOrganizationMembersQuery(id, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<OrganizationMemberDto>>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<PagedResult<OrganizationMemberDto>>.Fail(result.Error!));
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddOrganizationMemberCommand(id, request.UserId, request.IsDefault);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/organizations/{id}/members",
                    ApiEnvelope<OrganizationMemberDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<OrganizationMemberDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveOrganizationMemberCommand(id, userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for updating an organization.</summary>
public sealed record UpdateOrganizationRequest(
    string Name,
    string Timezone,
    string DefaultCurrency,
    string DefaultLanguage);

/// <summary>Request body for adding a member to an organization.</summary>
public sealed record AddMemberRequest(Guid UserId, bool IsDefault = false);
