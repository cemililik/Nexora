using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Api;

/// <summary>Minimal API endpoints for scheduled notifications.</summary>
public static class ScheduleEndpoints
{
    /// <summary>Maps schedule notification endpoints.</summary>
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/schedule")
            .RequireAuthorization();

        group.MapPost("/", async (ScheduleNotificationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            if (result.IsSuccess)
                return Results.Created(
                    $"/api/v1/notifications/schedule/{result.Value!.Id}",
                    ApiEnvelope<NotificationScheduleDto>.Success(result.Value, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_notifications_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<NotificationScheduleDto>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<NotificationScheduleDto>.Fail(result.Error))
            };
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CancelScheduledNotificationCommand(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<object>.Success(null!, result.Message))
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var query = new GetScheduledNotificationsQuery(page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<NotificationScheduleDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<NotificationScheduleDto>>.Fail(result.Error!));
        });
    }
}
