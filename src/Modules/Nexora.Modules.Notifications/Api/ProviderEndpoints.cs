using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Api;

/// <summary>Minimal API endpoints for notification provider management.</summary>
public static class ProviderEndpoints
{
    /// <summary>Maps notification provider CRUD and test endpoints.</summary>
    public static void MapProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/providers")
            .RequireAuthorization();

        group.MapGet("/", async (string? channel, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNotificationProvidersQuery(channel), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<NotificationProviderDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<IReadOnlyList<NotificationProviderDto>>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateNotificationProviderCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            if (result.IsSuccess)
                return Results.Created(
                    $"/api/v1/notifications/providers/{result.Value!.Id}",
                    ApiEnvelope<NotificationProviderDto>.Success(result.Value, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_notifications_error_provider_already_exists" =>
                    Results.Conflict(ApiEnvelope<NotificationProviderDto>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<NotificationProviderDto>.Fail(result.Error))
            };
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateProviderRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateNotificationProviderCommand(id, request.Config, request.DailyLimit, request.IsDefault);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<NotificationProviderDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<NotificationProviderDto>.Fail(result.Error!));
        });

        group.MapPost("/{id:guid}/test", async (Guid id, TestProviderRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new TestNotificationProviderCommand(id, request.TestRecipient);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<object>.Success(null!, result.Message))
                : Results.NotFound(ApiEnvelope<object>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for updating a notification provider.</summary>
public sealed record UpdateProviderRequest(string Config, int DailyLimit, bool IsDefault);

/// <summary>Request body for testing a notification provider.</summary>
public sealed record TestProviderRequest(string TestRecipient);
