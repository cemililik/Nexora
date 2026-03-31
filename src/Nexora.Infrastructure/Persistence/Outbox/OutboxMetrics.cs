using System.Diagnostics.Metrics;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// OpenTelemetry metrics for the Outbox/Inbox infrastructure.
/// Tracks enqueue, publish, failure, latency, and inbox duplicate counts.
/// </summary>
public static class OutboxMetrics
{
    private static readonly Meter Meter = new("Nexora.Infrastructure.Outbox");

    /// <summary>Total messages enqueued to the outbox.</summary>
    public static readonly Counter<long> MessagesEnqueued = Meter.CreateCounter<long>(
        "nexora.outbox.messages.enqueued", "messages", "Total messages enqueued to outbox");

    /// <summary>Total messages successfully published from the outbox.</summary>
    public static readonly Counter<long> MessagesPublished = Meter.CreateCounter<long>(
        "nexora.outbox.messages.published", "messages", "Total messages successfully published from outbox");

    /// <summary>Total message publish failures.</summary>
    public static readonly Counter<long> MessagesFailed = Meter.CreateCounter<long>(
        "nexora.outbox.messages.failed", "messages", "Total message publish failures");

    /// <summary>Time from enqueue to publish in milliseconds.</summary>
    public static readonly Histogram<double> ProcessingLatency = Meter.CreateHistogram<double>(
        "nexora.outbox.processing.latency_ms", "ms", "Time from enqueue to publish");

    /// <summary>Total duplicate events skipped by the inbox guard.</summary>
    public static readonly Counter<long> InboxDuplicatesSkipped = Meter.CreateCounter<long>(
        "nexora.inbox.duplicates.skipped", "messages", "Total duplicate events skipped by inbox guard");
}
