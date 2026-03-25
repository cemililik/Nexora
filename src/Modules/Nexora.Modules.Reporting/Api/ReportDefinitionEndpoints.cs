using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Api;

public static class ReportDefinitionEndpoints
{
    public static void MapReportDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/definitions")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, string? module, string? category, string? search,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetReportDefinitionsQuery(page ?? 1, pageSize ?? 20, module, category, search);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<ReportDefinitionDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<ReportDefinitionDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetReportDefinitionByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ReportDefinitionDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<ReportDefinitionDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateReportDefinitionCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/reporting/definitions/{result.Value!.Id}",
                    ApiEnvelope<ReportDefinitionDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ReportDefinitionDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateReportDefinitionRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateReportDefinitionCommand(
                id, request.Name, request.Description, request.Module,
                request.Category, request.QueryText, request.Parameters, request.DefaultFormat);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ReportDefinitionDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<ReportDefinitionDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteReportDefinitionCommand(id), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapPost("/test-query", async (TestQueryRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new TestReportQueryQuery(request.QueryText), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<TestReportQueryResultDto>.Success(result.Value!))
                : Results.BadRequest(ApiEnvelope<TestReportQueryResultDto>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for testing a SQL report query.</summary>
public sealed record TestQueryRequest(string QueryText);

/// <summary>Request body for updating an existing report definition.</summary>
public sealed record UpdateReportDefinitionRequest(
    string Name,
    string? Description,
    string Module,
    string? Category,
    string QueryText,
    string? Parameters,
    string DefaultFormat);
