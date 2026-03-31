using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Persists integration events to the outbox table for reliable, at-least-once delivery.
/// Serializes the event to JSON and stores it alongside metadata (type, tenant, timestamp).
/// </summary>
public sealed class OutboxService(
    OutboxDbContext dbContext,
    ILogger<OutboxService> logger) : IOutbox
{
    /// <inheritdoc />
    public async Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var eventType = typeof(TEvent).AssemblyQualifiedName
            ?? typeof(TEvent).FullName
            ?? typeof(TEvent).Name;

        var payload = JsonSerializer.Serialize(integrationEvent);
        var tenantId = integrationEvent.TenantId;

        var message = OutboxMessage.Create(eventType, payload, tenantId);

        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        OutboxMetrics.MessagesEnqueued.Add(1);

        logger.LogInformation(
            "Outbox message enqueued: {EventType} for tenant {TenantId} (MessageId: {MessageId})",
            typeof(TEvent).Name, tenantId, message.Id);
    }
}
