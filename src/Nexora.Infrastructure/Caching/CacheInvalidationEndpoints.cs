using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
    /// Maps the Dapr subscription endpoint that receives cache invalidation events
    /// from other instances and clears matching keys from the local L1 cache.
    /// </summary>
    public static IEndpointRouteBuilder MapCacheInvalidationSubscription(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/dapr/cache-invalidation", (
            CacheInvalidationEvent invalidationEvent,
            CacheInvalidationHandler handler) =>
        {
            handler.Handle(invalidationEvent);
            return Results.Ok();
        })
        .WithTopic("pubsub", CacheInvalidationHandler.TopicName)
        .AllowAnonymous()
        .ExcludeFromDescription();

        return endpoints;
    }
}
