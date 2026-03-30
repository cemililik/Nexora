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

        // Folder access endpoints

        group.MapGet("/{folderId:guid}/access", async (Guid folderId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFolderAccessQuery(folderId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<FolderAccessDto>>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<FolderAccessDto>>.Fail(result.Error!));
        })
        .WithSummary("Get folder access list")
        .WithDescription("Returns all active access permissions for the specified folder.")
        .Produces<ApiEnvelope<IReadOnlyList<FolderAccessDto>>>(StatusCodes.Status200OK)
        .Produces<ApiEnvelope<IReadOnlyList<FolderAccessDto>>>(StatusCodes.Status404NotFound);

        group.MapPost("/{folderId:guid}/access", async (Guid folderId, GrantFolderAccessRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new GrantFolderAccessCommand(folderId, request.UserId, request.RoleId, request.Permission, request.ExpiresAt);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/folders/{folderId}/access/{result.Value!.Id}",
                    ApiEnvelope<FolderAccessDto>.Success(result.Value, result.Message))
                : result.Error!.Message.Key == "lockey_documents_error_folder_not_found"
                    ? Results.NotFound(ApiEnvelope<FolderAccessDto>.Fail(result.Error))
                    : Results.BadRequest(ApiEnvelope<FolderAccessDto>.Fail(result.Error));
        })
        .WithSummary("Grant folder access")
        .WithDescription("Grants access permission on a folder to a user or role. Exactly one of userId or roleId must be provided.")
        .Produces<ApiEnvelope<FolderAccessDto>>(StatusCodes.Status201Created)
        .Produces<ApiEnvelope<FolderAccessDto>>(StatusCodes.Status400BadRequest)
        .Produces<ApiEnvelope<FolderAccessDto>>(StatusCodes.Status404NotFound);

        group.MapDelete("/{folderId:guid}/access/{accessId:guid}", async (Guid folderId, Guid accessId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RevokeFolderAccessCommand(folderId, accessId), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        })
        .WithSummary("Revoke folder access")
        .WithDescription("Revokes a previously granted access permission on a folder.")
        .Produces<ApiEnvelope<object>>(StatusCodes.Status200OK)
        .Produces<ApiEnvelope<object>>(StatusCodes.Status404NotFound);
    }
}

/// <summary>Request body for renaming a folder.</summary>
public sealed record RenameFolderRequest(string NewName);

/// <summary>Request body for granting folder access.</summary>
public sealed record GrantFolderAccessRequest(Guid? UserId, Guid? RoleId, string Permission, DateTimeOffset? ExpiresAt);
