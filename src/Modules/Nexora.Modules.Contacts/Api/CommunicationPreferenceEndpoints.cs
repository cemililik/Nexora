using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact communication preferences.</summary>
public static class CommunicationPreferenceEndpoints
{
    /// <summary>Maps communication preference endpoints.</summary>
    public static void MapCommunicationPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/preferences")
            .RequireAuthorization();

        group.MapGet("/", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCommunicationPreferencesQuery(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<CommunicationPreferenceDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<CommunicationPreferenceDto>>.Fail(result.Error!));
        });

        group.MapPut("/", async (Guid contactId, UpdatePreferencesRequest request, ISender sender, CancellationToken ct) =>
        {
            var preferences = request.Preferences
                .Select(p => new ChannelPreference(p.Channel, p.OptedIn, p.OptInSource))
                .ToList();
            var command = new UpdateCommunicationPreferencesCommand(contactId, preferences);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<CommunicationPreferenceDto>>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<CommunicationPreferenceDto>>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for updating communication preferences.</summary>
public sealed record UpdatePreferencesRequest(
    IReadOnlyList<ChannelPreferenceRequest> Preferences);

/// <summary>A single channel preference entry.</summary>
public sealed record ChannelPreferenceRequest(
    string Channel,
    bool OptedIn,
    string? OptInSource = null);
