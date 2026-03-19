using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for consent record management.</summary>
public static class ConsentEndpoints
{
    /// <summary>Maps consent endpoints.</summary>
    public static void MapConsentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/consents")
            .RequireAuthorization();

        group.MapGet("/", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactConsentsQuery(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<ConsentRecordDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<ConsentRecordDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid contactId, RecordConsentRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new RecordConsentCommand(
                contactId, request.ConsentType, request.Granted,
                request.Source, request.IpAddress);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/{contactId}/consents/{result.Value!.Id}",
                    ApiEnvelope<ConsentRecordDto>.Success(result.Value, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" => Results.NotFound(ApiEnvelope<ConsentRecordDto>.Fail(result.Error)),
                    "lockey_contacts_error_no_active_consent" => Results.NotFound(ApiEnvelope<ConsentRecordDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ConsentRecordDto>.Fail(result.Error))
                };
        });
    }
}

/// <summary>Request body for recording consent.</summary>
public sealed record RecordConsentRequest(
    string ConsentType,
    bool Granted,
    string? Source = null,
    string? IpAddress = null);
