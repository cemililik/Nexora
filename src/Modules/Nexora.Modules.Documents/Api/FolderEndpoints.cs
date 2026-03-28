using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Api;

/// <summary>Minimal API endpoints for folder management.</summary>
public static class FolderEndpoints
{
    /// <summary>Maps folder endpoints.</summary>
    public static void MapFolderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/folders")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? parentFolderId, string? moduleName, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFoldersQuery(parentFolderId, moduleName), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<FolderDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<IReadOnlyList<FolderDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFolderByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<FolderDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<FolderDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateFolderCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/folders/{result.Value!.Id}",
                    ApiEnvelope<FolderDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<FolderDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameFolderRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RenameFolderCommand(id, request.NewName), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<FolderDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key == "lockey_documents_error_folder_not_found"
                    ? Results.NotFound(ApiEnvelope<FolderDto>.Fail(result.Error))
                    : Results.BadRequest(ApiEnvelope<FolderDto>.Fail(result.Error));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteFolderCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_folder_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_documents_error_cannot_delete_system_folder" => Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_documents_error_folder_not_empty" => Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for renaming a folder.</summary>
public sealed record RenameFolderRequest(string NewName);
