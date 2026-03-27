using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact CRUD operations.</summary>
public static class ContactEndpoints
{
    /// <summary>Maps contact management endpoints.</summary>
    public static void MapContactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, string? search, string? status, string? type, Guid? tagId,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetContactsQuery(page ?? 1, pageSize ?? 20, search, status, type, tagId);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<ContactDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<ContactDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<ContactDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateContactCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/{result.Value!.Id}",
                    ApiEnvelope<ContactDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ContactDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateContactRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateContactCommand(
                id, request.FirstName, request.LastName, request.CompanyName,
                request.Email, request.Phone, request.Mobile, request.Website,
                request.TaxId, request.Language, request.Currency, request.Title);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<ContactDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ArchiveContactCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_contact_already_archived" => Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapGet("/{id:guid}/360", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContact360Query(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<Contact360Dto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<Contact360Dto>.Fail(result.Error!));
        });

        group.MapPost("/{id:guid}/restore", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RestoreContactCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_contact_not_archived" => Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for updating a contact.</summary>
public sealed record UpdateContactRequest(
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Website,
    string? TaxId,
    string Language,
    string Currency,
    string? Title = null);
