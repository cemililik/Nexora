using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Api;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dashboards")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var query = new GetDashboardsQuery(page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<DashboardDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<DashboardDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDashboardByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DashboardDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<DashboardDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateDashboardCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/reporting/dashboards/{result.Value!.Id}",
                    ApiEnvelope<DashboardDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<DashboardDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateDashboardRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateDashboardCommand(id, request.Name, request.Description, request.Widgets, request.IsDefault);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DashboardDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<DashboardDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteDashboardCommand(id), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapGet("/{dashboardId:guid}/widgets/{widgetId:guid}/data", async (
            Guid dashboardId, Guid widgetId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDashboardWidgetDataQuery(dashboardId, widgetId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<WidgetDataDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<WidgetDataDto>.Fail(result.Error!));
        });
    }
}

public sealed record UpdateDashboardRequest(
    string Name,
    string? Description,
    string? Widgets,
    bool IsDefault);
