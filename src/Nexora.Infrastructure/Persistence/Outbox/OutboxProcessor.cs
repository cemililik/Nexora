using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Npgsql;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Background service that polls outbox_messages from all tenant schemas for pending integration events,
/// deserializes them, and publishes via <see cref="IEventBus"/>.
/// Iterates all active tenants each cycle, using raw SQL to query tenant-specific outbox tables.
/// Processes messages individually so one failure does not block others.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;
    private static readonly ConcurrentDictionary<Type, MethodInfo> PublishMethodCache = new();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "OutboxProcessor started (PollingInterval: {PollingIntervalSeconds}s, BatchSize: {BatchSize}, MaxRetry: {MaxRetryCount})",
            _options.PollingIntervalSeconds, _options.BatchSize, _options.MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            int processedCount = 0;
            try
            {
                processedCount = await ProcessAllTenantsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor encountered an unexpected error during polling cycle");
            }

            if (processedCount < _options.BatchSize)
                await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("OutboxProcessor stopped");
    }

    private async Task<int> ProcessAllTenantsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<IActiveTenantProvider>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var connectionString = _options.ConnectionString;

        var tenants = await tenantProvider.GetActiveTenantsAsync(ct);
        var totalProcessed = 0;

        foreach (var tenant in tenants)
        {
            if (!Regex.IsMatch(tenant.SchemaName, @"^[a-z0-9_-]+$"))
            {
                logger.LogWarning("Skipping outbox processing for tenant with invalid schema name: {SchemaName}", tenant.SchemaName);
                continue;
            }

            try
            {
                var count = await ProcessTenantOutboxAsync(tenant.SchemaName, connectionString, eventBus, ct);
                totalProcessed += count;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Relation does not exist — tenant schema is not yet provisioned. Skip silently.
                logger.LogDebug("OutboxProcessor skipping tenant {SchemaName}: schema not provisioned yet", tenant.SchemaName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor failed to process outbox for tenant schema {SchemaName}", tenant.SchemaName);
            }
        }

        return totalProcessed;
    }

    private async Task<int> ProcessTenantOutboxAsync(
        string schemaName, string connectionString, IEventBus eventBus, CancellationToken ct)
    {
        // Fetch a batch of pending message IDs using FOR UPDATE SKIP LOCKED so
        // concurrent processor instances do not pick up the same rows.
        var messages = await FetchPendingMessagesAsync(schemaName, connectionString, ct);

        if (messages.Count == 0)
            return 0;

        logger.LogDebug("OutboxProcessor found {MessageCount} pending messages in schema {SchemaName}",
            messages.Count, schemaName);

        // Each message is processed in its own isolated transaction so that a single
        // failure does not roll back successfully published events within the same batch.
        foreach (var message in messages)
        {
            if (ct.IsCancellationRequested)
                break;

            await ProcessMessageAsync(schemaName, connectionString, eventBus, message, ct);
        }

        return messages.Count;
    }

    private async Task<List<(Guid Id, string EventType, string Payload, string TenantId, DateTimeOffset CreatedAt, int RetryCount)>> FetchPendingMessagesAsync(
        string schemaName, string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var selectSql = $"""
            SELECT "Id", "EventType", "EventPayload", "TenantId", "CreatedAt", "RetryCount"
            FROM "{schemaName}".outbox_messages
            WHERE "ProcessedAt" IS NULL AND "RetryCount" < @maxRetry
            ORDER BY "CreatedAt"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """;

        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var selectCmd = new NpgsqlCommand(selectSql, connection, tx);
        selectCmd.Parameters.AddWithValue("maxRetry", _options.MaxRetryCount);
        selectCmd.Parameters.AddWithValue("batchSize", _options.BatchSize);

        var messages = new List<(Guid Id, string EventType, string Payload, string TenantId, DateTimeOffset CreatedAt, int RetryCount)>();

        await using (var reader = await selectCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                messages.Add((
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetFieldValue<DateTimeOffset>(4),
                    reader.GetInt32(5)));
            }
        }

        // Commit the SELECT … FOR UPDATE SKIP LOCKED to release the advisory locks
        // after we have captured the IDs. Each message will be processed and
        // updated in its own independent transaction below.
        await tx.CommitAsync(CancellationToken.None);
        return messages;
    }

    private async Task ProcessMessageAsync(
        string schemaName, string connectionString, IEventBus eventBus,
        (Guid Id, string EventType, string Payload, string TenantId, DateTimeOffset CreatedAt, int RetryCount) message,
        CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        try
        {
            var eventType = Type.GetType(message.EventType);
            if (eventType is null)
            {
                logger.LogError(
                    "OutboxProcessor could not resolve event type {EventType} (MessageId: {MessageId}, TenantId: {TenantId})",
                    message.EventType, message.Id, message.TenantId);
                await RecordFailureAsync(connection, tx, schemaName, message.Id, $"Could not resolve type: {message.EventType}", message.RetryCount, ct);
                await tx.CommitAsync(CancellationToken.None);
                return;
            }

            var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType);
            if (integrationEvent is null)
            {
                logger.LogError(
                    "OutboxProcessor failed to deserialize event {EventType} (MessageId: {MessageId}, TenantId: {TenantId})",
                    message.EventType, message.Id, message.TenantId);
                await RecordFailureAsync(connection, tx, schemaName, message.Id, "Deserialization returned null", message.RetryCount, ct);
                await tx.CommitAsync(CancellationToken.None);
                return;
            }

            var publishMethod = PublishMethodCache.GetOrAdd(eventType, type =>
                typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))!.MakeGenericMethod(type));

            var task = (Task)publishMethod.Invoke(eventBus, [integrationEvent, ct])!;
            await task;

            await MarkProcessedAsync(connection, tx, schemaName, message.Id, ct);

            // Commit with CancellationToken.None so a shutdown signal does not
            // leave the database record in an inconsistent state after the event
            // has already been published to the bus.
            await tx.CommitAsync(CancellationToken.None);

            var latencyMs = (DateTimeOffset.UtcNow - message.CreatedAt).TotalMilliseconds;
            OutboxMetrics.MessagesPublished.Add(1);
            OutboxMetrics.ProcessingLatency.Record(latencyMs);

            logger.LogInformation(
                "OutboxProcessor published event {EventType} (MessageId: {MessageId}, TenantId: {TenantId}, LatencyMs: {LatencyMs})",
                eventType.Name, message.Id, message.TenantId, latencyMs);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            OutboxMetrics.MessagesFailed.Add(1);
            var newRetryCount = message.RetryCount + 1;

            if (newRetryCount >= _options.MaxRetryCount)
            {
                logger.LogError(ex,
                    "Outbox message {MessageId} exceeded max retries — moved to dead letter (EventType: {EventType}, TenantId: {TenantId}, RetryCount: {RetryCount})",
                    message.Id, message.EventType, message.TenantId, newRetryCount);
            }
            else
            {
                logger.LogWarning(ex,
                    "OutboxProcessor failed to process message {MessageId} (EventType: {EventType}, TenantId: {TenantId}, RetryCount: {RetryCount})",
                    message.Id, message.EventType, message.TenantId, newRetryCount);
            }

            await RecordFailureAsync(connection, tx, schemaName, message.Id, ex.Message, message.RetryCount, ct);
            await tx.CommitAsync(CancellationToken.None);
        }
    }

    private static async Task MarkProcessedAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, string schemaName, Guid messageId, CancellationToken ct)
    {
        var sql = $"""
            UPDATE "{schemaName}".outbox_messages
            SET "ProcessedAt" = @now
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("id", messageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task RecordFailureAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, string schemaName, Guid messageId, string error, int currentRetryCount, CancellationToken ct)
    {
        var sql = $"""
            UPDATE "{schemaName}".outbox_messages
            SET "RetryCount" = @retryCount, "Error" = @error
            WHERE "Id" = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("retryCount", currentRetryCount + 1);
        cmd.Parameters.AddWithValue("error", error);
        cmd.Parameters.AddWithValue("id", messageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
