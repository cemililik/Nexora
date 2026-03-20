using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Infrastructure.Webhooks;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Api;

/// <summary>Webhook callback endpoints for notification providers.</summary>
public static class WebhookEndpoints
{
    /// <summary>Maps provider webhook callback endpoints.</summary>
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/webhooks");

        group.MapPost("/sendgrid", async (HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);

            var events = WebhookPayloadParser.ParseSendGrid(payload);
            if (events.Count == 0)
                return Results.Ok();

            foreach (var webhookEvent in events)
            {
                // Find notification by scanning recipients — in production, use a lookup index
                var command = new UpdateDeliveryStatusCommand(
                    Guid.Empty, // Will be resolved by handler via provider message ID
                    webhookEvent.ProviderMessageId,
                    webhookEvent.Status,
                    webhookEvent.FailureReason);

                await sender.Send(command, ct);
            }

            return Results.Ok();
        });

        group.MapPost("/twilio", async (HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            var formData = new Dictionary<string, string>();
            var form = await request.ReadFormAsync(ct);
            foreach (var field in form)
            {
                formData[field.Key] = field.Value.ToString();
            }

            var webhookEvent = WebhookPayloadParser.ParseTwilio(formData);
            if (webhookEvent is null)
                return Results.Ok();

            var command = new UpdateDeliveryStatusCommand(
                Guid.Empty,
                webhookEvent.ProviderMessageId,
                webhookEvent.Status,
                webhookEvent.FailureReason);

            await sender.Send(command, ct);
            return Results.Ok();
        });
    }
}
