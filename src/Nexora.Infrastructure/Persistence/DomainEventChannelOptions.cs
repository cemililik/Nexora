using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Persistence;

/// <summary>Configuration for the domain event background dispatch channel.</summary>
public sealed class DomainEventChannelOptions
{
    /// <summary>Maximum number of events the bounded channel can hold. Default: 10 000.</summary>
    public int Capacity { get; set; } = 10_000;
}

/// <summary>Validates <see cref="DomainEventChannelOptions"/> for correctness.</summary>
public sealed class DomainEventChannelOptionsValidator
    : IValidateOptions<DomainEventChannelOptions>
{
    public ValidateOptionsResult Validate(string? name, DomainEventChannelOptions options)
    {
        if (options.Capacity <= 0)
            return ValidateOptionsResult.Fail("DomainEvents:Capacity must be greater than 0.");

        return ValidateOptionsResult.Success;
    }
}
