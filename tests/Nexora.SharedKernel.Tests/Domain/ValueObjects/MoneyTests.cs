using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Domain.ValueObjects;

namespace Nexora.SharedKernel.Tests.Domain.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Create_ShouldNormalizeCurrency()
    {
        var money = new Money(100.50m, "usd");

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_InvalidCurrency_ShouldThrow()
    {
        var act = () => new Money(10, "US");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = new Money(10, "USD");
        var b = new Money(20, "USD");

        var result = a.Add(b);

        result.Amount.Should().Be(30);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrow()
    {
        var usd = new Money(10, "USD");
        var eur = new Money(10, "EUR");

        var act = () => usd.Add(eur);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Subtract_ShouldReturnDifference()
    {
        var a = new Money(50, "TRY");
        var b = new Money(20, "TRY");

        var result = a.Subtract(b);

        result.Amount.Should().Be(30);
    }

    [Fact]
    public void Multiply_ShouldReturnProduct()
    {
        var money = new Money(10, "USD");

        var result = money.Multiply(3);

        result.Amount.Should().Be(30);
    }

    [Fact]
    public void Zero_ShouldCreateZeroAmount()
    {
        var money = Money.Zero("EUR");

        money.Amount.Should().Be(0);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new Money(10, "USD");
        var b = new Money(10, "USD");

        a.Should().Be(b);
    }
}
