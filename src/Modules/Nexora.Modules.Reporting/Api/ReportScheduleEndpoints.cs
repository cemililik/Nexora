using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Api;

public static class ReportScheduleEndpoints
{
    public static void MapReportScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/schedules")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? definitionId, int? page, int? pageSize,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetReportSchedulesQuery(definitionId, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<ReportScheduleDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<ReportScheduleDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateReportScheduleCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/reporting/schedules/{result.Value!.Id}",
                    ApiEnvelope<ReportScheduleDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ReportScheduleDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateReportScheduleRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateReportScheduleCommand(id, request.CronExpression, request.Format, request.Recipients);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ReportScheduleDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<ReportScheduleDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteReportScheduleCommand(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<object>.Success(null!, result.Message))
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}

public sealed record UpdateReportScheduleRequest(
    string CronExpression,
    string Format,
    string? Recipients);
