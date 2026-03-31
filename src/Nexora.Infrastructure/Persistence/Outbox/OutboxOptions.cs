namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Configuration options for the outbox processor.
/// Bound from the <c>Outbox</c> configuration section.
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the processor polls for pending messages, in seconds.</summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>Maximum number of messages to process per polling cycle.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Maximum retry attempts before a message is considered dead-lettered.</summary>
    public int MaxRetryCount { get; set; } = 10;

    /// <summary>Number of days after which processed messages are eligible for cleanup.</summary>
    public int CleanupAfterDays { get; set; } = 7;

    /// <summary>Pending message count above which the outbox is considered degraded.</summary>
    public int DegradedThreshold { get; set; } = 100;

    /// <summary>Pending message count above which the outbox is considered unhealthy.</summary>
    public int UnhealthyThreshold { get; set; } = 1000;
}
