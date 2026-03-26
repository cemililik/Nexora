namespace Nexora.Infrastructure.Persistence;

/// <summary>Configuration for the domain event background dispatch channel.</summary>
public sealed class DomainEventChannelOptions
{
    /// <summary>Maximum number of events the bounded channel can hold. Default: 10 000.</summary>
    public int Capacity { get; set; } = 10_000;
}
