using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Api;

/// <summary>Minimal API endpoints for notification sending and querying.</summary>
public static class NotificationEndpoints
{
    /// <summary>Maps notification send, list, and detail endpoints.</summary>
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/notifications")
            .RequireAuthorization();

        group.MapPost("/send", async (SendNotificationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            if (result.IsSuccess)
                return Results.Created(
                    $"/api/v1/notifications/notifications/{result.Value!.Id}",
                    ApiEnvelope<NotificationDto>.Success(result.Value, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_notifications_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<NotificationDto>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<NotificationDto>.Fail(result.Error))
            };
        });

        group.MapGet("/", async (int? page, int? pageSize, string? channel, string? status,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetNotificationsQuery(page ?? 1, pageSize ?? 20, channel, status);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<NotificationDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<NotificationDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNotificationByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<NotificationDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<NotificationDetailDto>.Fail(result.Error!));
        });
    }
}
