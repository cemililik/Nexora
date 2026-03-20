using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Api;

/// <summary>Minimal API endpoints for document CRUD operations.</summary>
public static class DocumentEndpoints
{
    /// <summary>Maps document management endpoints.</summary>
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/documents")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, Guid? folderId, string? search, string? status,
            Guid? linkedEntityId, string? linkedEntityType,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetDocumentsQuery(
                page ?? 1, pageSize ?? 20, folderId, search, status, linkedEntityId, linkedEntityType);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<DocumentDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<DocumentDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDocumentByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DocumentDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<DocumentDetailDto>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}/download", (Guid id) =>
        {
            // Phase 2: Actual MinIO download via presigned URL
            return Results.Json(
                ApiEnvelope<object>.Fail(new Error(Nexora.SharedKernel.Localization.LocalizedMessage.Of("lockey_documents_error_download_not_implemented"))),
                statusCode: StatusCodes.Status501NotImplemented);
        });

        group.MapPost("/", async (UploadDocumentCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/documents/{result.Value!.Id}",
                    ApiEnvelope<DocumentDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<DocumentDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateDocumentMetadataRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateDocumentMetadataCommand(id, request.Name, request.Description, request.Tags);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DocumentDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<DocumentDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ArchiveDocumentCommand(id), ct);
            if (result.IsSuccess)
                return Results.NoContent();

            return MapDocumentError(result.Error!);
        });

        group.MapPost("/{id:guid}/restore", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RestoreDocumentCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return MapDocumentError(result.Error!);
        });

        group.MapPost("/{id:guid}/move", async (Guid id, MoveDocumentRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MoveDocumentCommand(id, request.TargetFolderId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DocumentDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<DocumentDto>.Fail(result.Error!));
        });

        group.MapPost("/{id:guid}/link", async (Guid id, LinkDocumentRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LinkDocumentToEntityCommand(id, request.EntityId, request.EntityType), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<DocumentDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<DocumentDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}/link", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UnlinkDocumentFromEntityCommand(id), ct);
            if (result.IsSuccess)
                return Results.NoContent();

            return Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });
    }

    private static IResult MapDocumentError(Error error)
    {
        return error.Message.Key switch
        {
            "lockey_documents_error_document_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(error)),
            _ => Results.BadRequest(ApiEnvelope<object>.Fail(error))
        };
    }
}

/// <summary>Request body for updating document metadata.</summary>
public sealed record UpdateDocumentMetadataRequest(string Name, string? Description = null, string? Tags = null);

/// <summary>Request body for moving a document.</summary>
public sealed record MoveDocumentRequest(Guid TargetFolderId);

/// <summary>Request body for linking a document to an entity.</summary>
public sealed record LinkDocumentRequest(Guid EntityId, string EntityType);
