using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Audit.Application.Commands;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Api;

/// <summary>Minimal API endpoints for frontend-initiated auth event auditing.</summary>
public static class AuthEventEndpoints
{
    /// <summary>
    /// Maps POST /events/auth — any authenticated user can record an auth event for their
    /// own session (Login, Logout, PasswordChange, TokenRefresh, LoginFailed).
    /// </summary>
    public static void MapAuthEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/events/auth", async (
            RecordAuthEventRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RecordAuthEventCommand(request.EventType, request.IsSuccess, request.Metadata);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope<object>.Fail(result.Error!));
        })
        .RequireAuthorization()
        .Produces<ApiEnvelope<string>>(StatusCodes.Status200OK)
        .Produces<ApiEnvelope<object>>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status500InternalServerError)
        .WithSummary("Record an auth event")
        .WithDescription("Records a Login, Logout, PasswordChange, TokenRefresh, or LoginFailed event for the current user.");
    }
}

/// <summary>Request body for POST /audit/events/auth — accepted from admin and portal frontends.</summary>
public sealed record RecordAuthEventRequest(
    AuthEventType EventType,
    bool IsSuccess,
    string? Metadata = null);
