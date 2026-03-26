using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Background service that processes domain events queued via <see cref="DomainEventChannel"/>.
/// Creates a new DI scope per event to ensure scoped dependencies (DbContext, IEventBus, etc.)
/// are properly resolved — the processor itself is a singleton.
/// Retries transient failures before logging and dropping the event.
/// </summary>
public sealed class DomainEventBackgroundProcessor(
    DomainEventChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<DomainEventBackgroundProcessor> logger) : BackgroundService
{
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var domainEvent in channel.ReadAllAsync(stoppingToken))
        {
            await DispatchWithRetryAsync(domainEvent, stoppingToken);
        }
    }

    private async Task DispatchWithRetryAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var eventType = domainEvent.GetType().Name;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(domainEvent, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // Let BackgroundService handle graceful shutdown
            }
            catch (Exception ex) when (!IsTransient(ex))
            {
                logger.LogError(ex,
                    "Domain event {EventType} dispatch failed with non-transient error — event dropped",
                    eventType);
                return;
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries)
                {
                    logger.LogError(ex,
                        "Domain event {EventType} dispatch failed after {MaxRetries} attempts — event dropped",
                        eventType, MaxRetries);
                    return;
                }

                var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
                logger.LogWarning(ex,
                    "Domain event {EventType} dispatch failed (attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms",
                    eventType, attempt, MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex is
        TimeoutException or
        HttpRequestException or
        IOException;
}
