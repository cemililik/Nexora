using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact activity timeline.</summary>
public static class ContactActivityEndpoints
{
    /// <summary>Maps contact activity endpoints.</summary>
    public static void MapContactActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/activities")
            .RequireAuthorization();

        group.MapGet("/", async (Guid contactId, string? moduleSource, int? take, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactActivitiesQuery(contactId, moduleSource, take), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<ContactActivityDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<ContactActivityDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (Guid contactId, LogActivityRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new LogContactActivityCommand(
                contactId, request.ModuleSource, request.ActivityType,
                request.Summary, request.Details);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/{contactId}/activities/{result.Value!.Id}",
                    ApiEnvelope<ContactActivityDto>.Success(result.Value, result.Message))
                : Results.NotFound(ApiEnvelope<ContactActivityDto>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for logging an activity.</summary>
public sealed record LogActivityRequest(
    string ModuleSource,
    string ActivityType,
    string Summary,
    string? Details = null);
