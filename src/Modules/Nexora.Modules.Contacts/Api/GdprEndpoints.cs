using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for GDPR compliance operations.</summary>
public static class GdprEndpoints
{
    /// <summary>Maps GDPR endpoints.</summary>
    public static void MapGdprEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts/{contactId:guid}/gdpr")
            .RequireAuthorization();

        group.MapPost("/export", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RequestGdprExportCommand(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<GdprExportDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" =>
                        Results.NotFound(ApiEnvelope<GdprExportDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<GdprExportDto>.Fail(result.Error))
                };
        });

        group.MapPost("/delete", async (Guid contactId, GdprDeleteRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new RequestGdprDeleteCommand(contactId, request.Reason);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" =>
                        Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                    "lockey_contacts_error_gdpr_delete_merged_contact" =>
                        Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
                };
        });
    }
}

/// <summary>Request body for GDPR delete.</summary>
public sealed record GdprDeleteRequest(string Reason);
