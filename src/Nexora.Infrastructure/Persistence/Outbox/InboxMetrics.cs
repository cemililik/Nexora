using System.Diagnostics.Metrics;

namespace Nexora.Infrastructure.Persistence.Inbox;

/// <summary>
/// OpenTelemetry metrics for the Inbox idempotency guard.
/// Tracks duplicate events skipped during integration event consumption.
/// </summary>
public static class InboxMetrics
{
    private static readonly Meter _meter = new("Nexora.Infrastructure.Inbox", "1.0");

    /// <summary>Total duplicate events skipped by the inbox guard.</summary>
    public static readonly Counter<long> DuplicatesSkipped = _meter.CreateCounter<long>(
        "nexora.inbox.duplicates.skipped", "messages", "Total duplicate events skipped by inbox guard");
}
