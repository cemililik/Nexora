using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact relationship management.</summary>
public static class ContactRelationshipEndpoints
{
    /// <summary>Maps contact relationship endpoints.</summary>
    public static void MapContactRelationshipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/relationships")
            .RequireAuthorization();

        group.MapGet("/", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactRelationshipsQuery(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<ContactRelationshipDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<ContactRelationshipDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid contactId, AddRelationshipRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddContactRelationshipCommand(contactId, request.RelatedContactId, request.Type);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/{contactId}/relationships/{result.Value!.Id}",
                    ApiEnvelope<ContactRelationshipDto>.Success(result.Value, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<ContactRelationshipDto>.Fail(result.Error)),
                    "lockey_contacts_error_related_contact_not_found" => Results.NotFound(ApiEnvelope<ContactRelationshipDto>.Fail(result.Error)),
                    "lockey_contacts_error_relationship_already_exists" => Results.Conflict(ApiEnvelope<ContactRelationshipDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ContactRelationshipDto>.Fail(result.Error))
                };
        });

        group.MapDelete("/{relationshipId:guid}", async (Guid contactId, Guid relationshipId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveContactRelationshipCommand(contactId, relationshipId), ct);
            if (result.IsSuccess)
                return Results.NoContent();

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_relationship_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for adding a relationship.</summary>
public sealed record AddRelationshipRequest(
    Guid RelatedContactId,
    string Type);
