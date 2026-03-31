using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Nexora.Infrastructure.Caching;

/// <summary>
/// Maps the Dapr pub/sub subscription endpoint for cross-instance cache invalidation.
/// Dapr's <c>MapSubscribeHandler</c> exposes a <c>/dapr/subscribe</c> endpoint that returns
/// the list of programmatic subscriptions. This class registers a handler endpoint
/// that Dapr routes pub/sub messages to.
/// </summary>
public static class CacheInvalidationEndpoints
{
    /// <summary>
    /// The expected value of the <c>dapr-app-id</c> header injected by the Dapr sidecar.
    /// Only requests originating from the local sidecar carry this header.
    /// </summary>
    internal const string DaprAppIdHeader = "dapr-app-id";

    /// <summary>
    /// Maps the Dapr subscription endpoint that receives cache invalidation events
    /// from other instances and clears matching keys from the local L1 cache.
    /// </summary>
    public static IEndpointRouteBuilder MapCacheInvalidationSubscription(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/dapr/cache-invalidation", (
            HttpContext httpContext,
            CacheInvalidationEvent invalidationEvent,
            CacheInvalidationHandler handler,
            ILogger<CacheInvalidationHandler> logger) =>
        {
            // Dapr sidecar always injects the dapr-app-id header when forwarding
            // pub/sub messages. Requests missing this header did not originate from
            // the local sidecar and should be rejected. In production, Kubernetes
            // NetworkPolicy additionally restricts port 3500 to the pod's own sidecar,
            // providing defence-in-depth.
            if (!httpContext.Request.Headers.ContainsKey(DaprAppIdHeader))
            {
                logger.LogWarning("Cache invalidation request rejected: missing {Header} header", DaprAppIdHeader);
                return Results.Unauthorized();
            }

            handler.Handle(invalidationEvent);
            return Results.Ok();
        })
        .WithTopic("pubsub", CacheInvalidationHandler.TopicName)
        .AllowAnonymous() // JWT is absent; Dapr header + NetworkPolicy enforce origin
        .ExcludeFromDescription();

        return endpoints;
    }
}
