using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for tag CRUD and contact tag assignment.</summary>
public static class TagEndpoints
{
    /// <summary>Maps tag management endpoints.</summary>
    public static void MapTagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tagGroup = endpoints.MapGroup("/tags")
            .RequireAuthorization();

        tagGroup.MapGet("/", async (
            string? category, bool? isActive,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetTagsQuery(category, isActive);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<TagDto>>.Success(result.Value!))
                : Results.BadRequest(ApiEnvelope<IReadOnlyList<TagDto>>.Fail(result.Error!));
        });

        tagGroup.MapPost("/", async (CreateTagCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/tags/{result.Value!.Id}",
                    ApiEnvelope<TagDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<TagDto>.Fail(result.Error!));
        });

        tagGroup.MapPut("/{id:guid}", async (Guid id, UpdateTagRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateTagCommand(id, request.Name, request.Category, request.Color);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<TagDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<TagDto>.Fail(result.Error!));
        });

        tagGroup.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteTagCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_tag_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_tag_already_deactivated" => Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        // Contact tag assignment endpoints
        var contactTagGroup = endpoints.MapGroup("/contacts/{contactId:guid}/tags")
            .RequireAuthorization();

        contactTagGroup.MapPost("/{tagId:guid}", async (Guid contactId, Guid tagId, ISender sender, CancellationToken ct) =>
        {
            var command = new AddTagToContactCommand(contactId, tagId);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/{contactId}/tags/{tagId}",
                    ApiEnvelope<ContactTagDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<ContactTagDto>.Fail(result.Error)),
                    "lockey_contacts_error_tag_not_found" => Results.NotFound(ApiEnvelope<ContactTagDto>.Fail(result.Error)),
                    "lockey_contacts_error_tag_already_assigned" => Results.Conflict(ApiEnvelope<ContactTagDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ContactTagDto>.Fail(result.Error))
                };
        });

        contactTagGroup.MapDelete("/{tagId:guid}", async (Guid contactId, Guid tagId, ISender sender, CancellationToken ct) =>
        {
            var command = new RemoveTagFromContactCommand(contactId, tagId);
            var result = await sender.Send(command, ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_tag_not_assigned" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for updating a tag.</summary>
public sealed record UpdateTagRequest(
    string Name,
    string Category,
    string? Color);
