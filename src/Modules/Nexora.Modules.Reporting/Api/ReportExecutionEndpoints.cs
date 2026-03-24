using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Api;

public static class ReportExecutionEndpoints
{
    public static void MapReportExecutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/reporting/executions")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? definitionId, string? status, int? page, int? pageSize,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetReportExecutionsQuery(definitionId, status, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<ReportExecutionDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<ReportExecutionDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetReportExecutionByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ReportExecutionDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<ReportExecutionDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (ExecuteReportCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/reporting/executions/{result.Value!.Id}",
                    ApiEnvelope<ReportExecutionDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ReportExecutionDto>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}/download", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DownloadReportResultQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DownloadReportResultDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<DownloadReportResultDto>.Fail(result.Error!));
        });
    }
}
