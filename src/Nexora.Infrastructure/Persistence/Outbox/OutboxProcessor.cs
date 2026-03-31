using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Background service that polls the outbox_messages table for pending integration events,
/// deserializes them, and publishes via <see cref="IEventBus"/>.
/// Creates a new DI scope per polling cycle to ensure scoped dependencies are properly resolved.
/// Processes messages individually so one failure does not block others.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "OutboxProcessor started (PollingInterval: {PollingIntervalSeconds}s, BatchSize: {BatchSize}, MaxRetry: {MaxRetryCount})",
            _options.PollingIntervalSeconds, _options.BatchSize, _options.MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor encountered an unexpected error during polling cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("OutboxProcessor stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < _options.MaxRetryCount)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        logger.LogDebug("OutboxProcessor found {MessageCount} pending messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType is null)
                {
                    logger.LogError(
                        "OutboxProcessor could not resolve event type {EventType} (MessageId: {MessageId}, TenantId: {TenantId})",
                        message.EventType, message.Id, message.TenantId);
                    message.RecordFailure($"Could not resolve type: {message.EventType}");
                    await dbContext.SaveChangesAsync(ct);
                    continue;
                }

                var integrationEvent = JsonSerializer.Deserialize(message.EventPayload, eventType);
                if (integrationEvent is null)
                {
                    logger.LogError(
                        "OutboxProcessor failed to deserialize event {EventType} (MessageId: {MessageId}, TenantId: {TenantId})",
                        message.EventType, message.Id, message.TenantId);
                    message.RecordFailure("Deserialization returned null");
                    await dbContext.SaveChangesAsync(ct);
                    continue;
                }

                // Use reflection to call IEventBus.PublishAsync<TEvent> with the concrete type
                var publishMethod = typeof(IEventBus)
                    .GetMethod(nameof(IEventBus.PublishAsync))!
                    .MakeGenericMethod(eventType);

                var task = (Task)publishMethod.Invoke(eventBus, [integrationEvent, ct])!;
                await task;

                message.MarkProcessed();
                await dbContext.SaveChangesAsync(ct);

                var latencyMs = (DateTimeOffset.UtcNow - message.CreatedAt).TotalMilliseconds;
                OutboxMetrics.MessagesPublished.Add(1);
                OutboxMetrics.ProcessingLatency.Record(latencyMs);

                logger.LogInformation(
                    "OutboxProcessor published event {EventType} (MessageId: {MessageId}, TenantId: {TenantId}, LatencyMs: {LatencyMs})",
                    eventType.Name, message.Id, message.TenantId, latencyMs);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // Let the outer loop handle shutdown
            }
            catch (Exception ex)
            {
                message.RecordFailure(ex.Message);
                OutboxMetrics.MessagesFailed.Add(1);

                if (message.RetryCount >= _options.MaxRetryCount)
                {
                    logger.LogError(ex,
                        "Outbox message {MessageId} exceeded max retries — moved to dead letter (EventType: {EventType}, TenantId: {TenantId}, RetryCount: {RetryCount})",
                        message.Id, message.EventType, message.TenantId, message.RetryCount);
                }
                else
                {
                    logger.LogWarning(ex,
                        "OutboxProcessor failed to process message {MessageId} (EventType: {EventType}, TenantId: {TenantId}, RetryCount: {RetryCount})",
                        message.Id, message.EventType, message.TenantId, message.RetryCount);
                }

                await dbContext.SaveChangesAsync(ct);
            }
        }
    }
}
