using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Domain.ValueObjects;

namespace Nexora.SharedKernel.Tests.Domain.ValueObjects;

public sealed class PhoneNumberTests
{
    [Fact]
    public void Create_ValidPhone_ShouldSucceed()
    {
        var phone = new PhoneNumber("90", "5551234567");

        phone.CountryCode.Should().Be("90");
        phone.Number.Should().Be("5551234567");
    }

    [Fact]
    public void Create_ShouldStripPlusFromCountryCode()
    {
        var phone = new PhoneNumber("+1", "5551234567");

        phone.CountryCode.Should().Be("1");
    }

    [Fact]
    public void Create_EmptyCountryCode_ShouldThrow()
    {
        var act = () => new PhoneNumber("", "5551234567");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_EmptyNumber_ShouldThrow()
    {
        var act = () => new PhoneNumber("90", "");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToString_ShouldReturnFormattedNumber()
    {
        var phone = new PhoneNumber("90", "5551234567");

        phone.ToString().Should().Be("+905551234567");
    }
}
