using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Api;

/// <summary>Minimal API endpoints for document version management.</summary>
public static class DocumentVersionEndpoints
{
    /// <summary>Maps document version endpoints.</summary>
    public static void MapDocumentVersionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/documents/{documentId:guid}/versions")
            .RequireAuthorization();

        group.MapGet("/", async (Guid documentId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDocumentVersionsQuery(documentId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<DocumentVersionDto>>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<DocumentVersionDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid documentId, AddVersionRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddDocumentVersionCommand(documentId, request.StorageKey, request.FileSize, request.ChangeNote);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/{documentId}/versions/{result.Value!.Id}",
                    ApiEnvelope<DocumentVersionDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<DocumentVersionDto>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for adding a document version.</summary>
public sealed record AddVersionRequest(string StorageKey, long FileSize, string? ChangeNote = null);
