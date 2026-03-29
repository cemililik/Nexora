using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Api;

/// <summary>Minimal API endpoints for audit log querying.</summary>
public static class AuditLogEndpoints
{
    /// <summary>Maps audit log list and detail endpoints.</summary>
    public static void MapAuditLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/logs")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, string? module, string? operation,
            Guid? userId, string? entityType, bool? isSuccess,
            DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetAuditLogsQuery(
                page ?? 1, pageSize ?? 20,
                module, operation, userId, entityType, isSuccess,
                dateFrom, dateTo);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<AuditLogDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<AuditLogDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAuditLogDetailQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<AuditLogDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<AuditLogDetailDto>.Fail(result.Error!));
        });
    }
}
