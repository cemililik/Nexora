using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Api;

/// <summary>Minimal API endpoints for document template operations.</summary>
public static class TemplateEndpoints
{
    /// <summary>Maps document template management endpoints.</summary>
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/templates")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, string? category, bool? isActive,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetDocumentTemplatesQuery(page ?? 1, pageSize ?? 20, category, isActive);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<DocumentTemplateDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<DocumentTemplateDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDocumentTemplateByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DocumentTemplateDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<DocumentTemplateDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateDocumentTemplateCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/templates/{result.Value!.Id}",
                    ApiEnvelope<DocumentTemplateDetailDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<DocumentTemplateDetailDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTemplateRequest body, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateDocumentTemplateCommand(id, body.Name, body.Category, body.Format, body.VariableDefinitions);
            var result = await sender.Send(command, ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<DocumentTemplateDetailDto>.Success(result.Value!, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<DocumentTemplateDetailDto>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<DocumentTemplateDetailDto>.Fail(result.Error))
            };
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ActivateDocumentTemplateCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeactivateDocumentTemplateCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapPost("/{id:guid}/render", async (Guid id, RenderTemplateRequest body, ISender sender, CancellationToken ct) =>
        {
            var command = new RenderDocumentTemplateCommand(id, body.FolderId, body.OutputName, body.Variables);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/{result.Value!.DocumentId}",
                    ApiEnvelope<RenderTemplateResultDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<RenderTemplateResultDto>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for updating a template.</summary>
public sealed record UpdateTemplateRequest(string Name, string Category, string Format, string? VariableDefinitions = null);

/// <summary>Request body for rendering a template.</summary>
public sealed record RenderTemplateRequest(Guid FolderId, string OutputName, Dictionary<string, string> Variables);
