using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact address management.</summary>
public static class ContactAddressEndpoints
{
    /// <summary>Maps contact address endpoints.</summary>
    public static void MapContactAddressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/addresses")
            .RequireAuthorization();

        group.MapGet("/", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactAddressesQuery(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<ContactAddressDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<ContactAddressDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid contactId, AddAddressRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new AddContactAddressCommand(
                contactId, request.Type, request.Street1, request.City, request.CountryCode,
                request.Street2, request.State, request.PostalCode, request.IsPrimary);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/{contactId}/addresses/{result.Value!.Id}",
                    ApiEnvelope<ContactAddressDto>.Success(result.Value, result.Message))
                : Results.NotFound(ApiEnvelope<ContactAddressDto>.Fail(result.Error!));
        });

        group.MapPut("/{addressId:guid}", async (
            Guid contactId, Guid addressId, UpdateAddressRequest request,
            ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateContactAddressCommand(
                contactId, addressId, request.Type, request.Street1, request.City, request.CountryCode,
                request.Street2, request.State, request.PostalCode);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactAddressDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<ContactAddressDto>.Fail(result.Error!));
        });

        group.MapDelete("/{addressId:guid}", async (Guid contactId, Guid addressId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveContactAddressCommand(contactId, addressId), ct);
            if (result.IsSuccess)
                return Results.NoContent();

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_address_not_found" => Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for adding an address.</summary>
public sealed record AddAddressRequest(
    string Type,
    string Street1,
    string City,
    string CountryCode,
    string? Street2 = null,
    string? State = null,
    string? PostalCode = null,
    bool IsPrimary = false);

/// <summary>Request body for updating an address.</summary>
public sealed record UpdateAddressRequest(
    string Type,
    string Street1,
    string City,
    string CountryCode,
    string? Street2 = null,
    string? State = null,
    string? PostalCode = null);
