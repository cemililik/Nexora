using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>Minimal API endpoints for audit log management.</summary>
public static class AuditEndpoints
{
    /// <summary>Maps audit log query and recording endpoints.</summary>
    public static void MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/audit-logs")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? userId, string? action,
            DateTimeOffset? from, DateTimeOffset? to,
            int? page, int? pageSize,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetAuditLogsQuery(userId, action, from, to, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<AuditLogDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<AuditLogDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (RecordAuditLogCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created("/api/v1/identity/audit-logs", ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}
