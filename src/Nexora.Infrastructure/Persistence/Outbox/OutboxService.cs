using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Persists integration events to the outbox table for reliable, at-least-once delivery.
/// Serializes the event to JSON and adds it to the caller's DbContext change tracker.
/// The message is saved atomically with the business data when the caller's SaveChangesAsync is invoked.
/// Generic over TContext so each module uses its own DbContext, ensuring transactional atomicity.
/// </summary>
public sealed class OutboxService<TContext>(
    TContext dbContext,
    ILogger<OutboxService<TContext>> logger) : IOutbox
    where TContext : DbContext
{
    /// <inheritdoc />
    public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var eventType = typeof(TEvent).AssemblyQualifiedName
            ?? typeof(TEvent).FullName
            ?? typeof(TEvent).Name;

        var payload = JsonSerializer.Serialize(integrationEvent);
        var tenantId = integrationEvent.TenantId;

        var message = OutboxMessage.Create(eventType, payload, tenantId);

        dbContext.Set<OutboxMessage>().Add(message);
        // No SaveChangesAsync — saved atomically with caller's unit of work

        OutboxMetrics.MessagesEnqueued.Add(1);

        logger.LogDebug(
            "Outbox message staged: {EventType} for tenant {TenantId} (MessageId: {MessageId})",
            typeof(TEvent).Name, tenantId, message.Id);

        return Task.CompletedTask;
    }
}
