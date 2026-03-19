using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact note management.</summary>
public static class ContactNoteEndpoints
{
    /// <summary>Maps contact note endpoints.</summary>
    public static void MapContactNoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/notes")
            .RequireAuthorization();

        group.MapGet("/", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactNotesQuery(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<ContactNoteDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<ContactNoteDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid contactId, AddNoteRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddContactNoteCommand(contactId, request.AuthorUserId, request.Content);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/{contactId}/notes/{result.Value!.Id}",
                    ApiEnvelope<ContactNoteDto>.Success(result.Value, result.Message))
                : Results.NotFound(ApiEnvelope<ContactNoteDto>.Fail(result.Error!));
        });

        group.MapPut("/{noteId:guid}", async (Guid contactId, Guid noteId, UpdateNoteRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateContactNoteCommand(contactId, noteId, request.Content);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactNoteDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<ContactNoteDto>.Fail(result.Error)),
                    "lockey_contacts_error_note_not_found" => Results.NotFound(ApiEnvelope<ContactNoteDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ContactNoteDto>.Fail(result.Error))
                };
        });

        group.MapDelete("/{noteId:guid}", async (Guid contactId, Guid noteId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteContactNoteCommand(contactId, noteId), ct);
            if (result.IsSuccess)
                return Results.NoContent();

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_note_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapPatch("/{noteId:guid}/pin", async (Guid contactId, Guid noteId, PinNoteRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new PinContactNoteCommand(contactId, noteId, request.Pin);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactNoteDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<ContactNoteDto>.Fail(result.Error)),
                    "lockey_contacts_error_note_not_found" => Results.NotFound(ApiEnvelope<ContactNoteDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ContactNoteDto>.Fail(result.Error))
                };
        });
    }
}

/// <summary>Request body for adding a note.</summary>
public sealed record AddNoteRequest(Guid AuthorUserId, string Content);

/// <summary>Request body for updating a note.</summary>
public sealed record UpdateNoteRequest(string Content);

/// <summary>Request body for pinning/unpinning a note.</summary>
public sealed record PinNoteRequest(bool Pin);
