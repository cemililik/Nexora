using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Api;

/// <summary>Minimal API endpoints for document access management.</summary>
public static class DocumentAccessEndpoints
{
    /// <summary>Maps document access permission endpoints.</summary>
    public static void MapDocumentAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/documents/{documentId:guid}/access")
            .RequireAuthorization();

        group.MapGet("/", async (Guid documentId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDocumentAccessQuery(documentId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<DocumentAccessDto>>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<DocumentAccessDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid documentId, GrantAccessRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new GrantDocumentAccessCommand(documentId, request.UserId, request.RoleId, request.Permission);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/{documentId}/access/{result.Value!.Id}",
                    ApiEnvelope<DocumentAccessDto>.Success(result.Value, result.Message))
                : result.Error!.Message.Key == "lockey_documents_error_document_not_found"
                    ? Results.NotFound(ApiEnvelope<DocumentAccessDto>.Fail(result.Error))
                    : Results.BadRequest(ApiEnvelope<DocumentAccessDto>.Fail(result.Error));
        });

        group.MapDelete("/{accessId:guid}", async (Guid documentId, Guid accessId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RevokeDocumentAccessCommand(documentId, accessId), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for granting document access.</summary>
public sealed record GrantAccessRequest(Guid? UserId, Guid? RoleId, string Permission);
