using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Value object for monetary amounts. Enforces currency consistency.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("lockey_shared_money_invalid_currency");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>Creates a zero-amount Money in the specified currency.</summary>
    public static Money Zero(string currency) => new(0, currency);

    /// <summary>Adds another Money value. Both must share the same currency.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts another Money value. Both must share the same currency.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Multiplies the amount by the given factor.</summary>
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("lockey_shared_money_currency_mismatch");
    }
}
