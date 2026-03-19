using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Domain.ValueObjects;

namespace Nexora.SharedKernel.Tests.Domain.ValueObjects;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("User@Example.COM")]
    [InlineData("test.user+tag@domain.co")]
    public void Create_ValidEmail_ShouldSucceed(string email)
    {
        var address = new EmailAddress(email);

        address.Value.Should().Be(email.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@missing-local.com")]
    [InlineData("missing-domain@")]
    public void Create_InvalidEmail_ShouldThrow(string email)
    {
        var act = () => new EmailAddress(email);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var email = new EmailAddress("Test@Example.com");

        email.ToString().Should().Be("test@example.com");
    }

    [Fact]
    public void Equality_SameEmail_ShouldBeEqual()
    {
        var a = new EmailAddress("user@test.com");
        var b = new EmailAddress("USER@TEST.COM");

        a.Should().Be(b);
    }
}
