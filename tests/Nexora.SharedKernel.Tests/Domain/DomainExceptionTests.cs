using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.SharedKernel.Tests.Domain;

public sealed class DomainExceptionTests
{
    [Fact]
    public void Create_WithLockeyPrefix_ShouldSucceed()
    {
        var ex = new DomainException("lockey_test_error");

        ex.Message.Should().Be("lockey_test_error");
    }

    [Fact]
    public void Create_WithoutLockeyPrefix_ShouldThrow()
    {
        var act = () => new DomainException("invalid_key");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*lockey_*");
    }
}
