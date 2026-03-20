using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Api;

/// <summary>Minimal API endpoints for bulk notification sending.</summary>
public static class BulkEndpoints
{
    /// <summary>Maps bulk notification endpoints.</summary>
    public static void MapBulkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bulk")
            .RequireAuthorization();

        group.MapPost("/", async (SendBulkNotificationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<BulkNotificationResultDto>.Success(result.Value!, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_notifications_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<BulkNotificationResultDto>.Fail(result.Error)),
                "lockey_notifications_error_bulk_no_valid_recipients" =>
                    Results.BadRequest(ApiEnvelope<BulkNotificationResultDto>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<BulkNotificationResultDto>.Fail(result.Error))
            };
        });
    }
}
