using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Api;

/// <summary>Minimal API endpoints for notification template management.</summary>
public static class TemplateEndpoints
{
    /// <summary>Maps notification template CRUD and translation endpoints.</summary>
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/templates")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, string? channel, string? module, bool? isActive,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetNotificationTemplatesQuery(page ?? 1, pageSize ?? 20, channel, module, isActive);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<NotificationTemplateDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<NotificationTemplateDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNotificationTemplateByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<NotificationTemplateDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<NotificationTemplateDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateNotificationTemplateCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            if (result.IsSuccess)
                return Results.Created(
                    $"/api/v1/notifications/templates/{result.Value!.Id}",
                    ApiEnvelope<NotificationTemplateDto>.Success(result.Value, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_notifications_error_template_code_exists" =>
                    Results.Conflict(ApiEnvelope<NotificationTemplateDto>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<NotificationTemplateDto>.Fail(result.Error))
            };
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateNotificationTemplateRequest request,
            ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateNotificationTemplateCommand(id, request.Subject, request.Body, request.Format);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<NotificationTemplateDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<NotificationTemplateDto>.Fail(result.Error!));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteNotificationTemplateCommand(id), ct);

            if (result.IsSuccess)
                return Results.NoContent();

            return result.Error!.Message.Key switch
            {
                "lockey_notifications_error_template_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_notifications_error_cannot_delete_system_template" =>
                    Results.BadRequest(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapPost("/{id:guid}/translations", async (Guid id, AddTranslationRequest request,
            ISender sender, CancellationToken ct) =>
        {
            var command = new AddTemplateTranslationCommand(id, request.LanguageCode, request.Subject, request.Body);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<NotificationTemplateTranslationDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<NotificationTemplateTranslationDto>.Fail(result.Error!));
        });
    }
}

/// <summary>Request body for updating a notification template.</summary>
public sealed record UpdateNotificationTemplateRequest(string Subject, string Body, string Format);

/// <summary>Request body for adding a template translation.</summary>
public sealed record AddTranslationRequest(string LanguageCode, string Subject, string Body);
