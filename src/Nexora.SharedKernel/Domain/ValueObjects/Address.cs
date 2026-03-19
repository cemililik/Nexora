namespace Nexora.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Value object for physical addresses.
/// </summary>
public sealed record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);
