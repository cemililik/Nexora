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
/// Minimal API endpoints for tenant management.
/// </summary>
public static class TenantEndpoints
{
    /// <summary>Maps tenant CRUD and status endpoints.</summary>
    public static void MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var query = new GetTenantsQuery(page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<TenantDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<TenantDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var query = new GetTenantByIdQuery(id);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<TenantDetailDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<TenantDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateTenantCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/tenants/{result.Value!.Id}",
                    ApiEnvelope<TenantDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<TenantDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}/status", async (Guid id, UpdateTenantStatusRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateTenantStatusCommand(id, request.Action);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}

public sealed record UpdateTenantStatusRequest(string Action);
