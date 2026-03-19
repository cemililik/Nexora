using Nexora.SharedKernel.Localization;

namespace Nexora.SharedKernel.Tests.Localization;

public sealed class LocalizedMessageTests
{
    [Fact]
    public void Create_WithValidKey_ShouldSucceed()
    {
        var msg = new LocalizedMessage("lockey_test_hello");

        msg.Key.Should().Be("lockey_test_hello");
        msg.Params.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithParams_ShouldStoreParams()
    {
        var msg = new LocalizedMessage("lockey_test_greeting",
            new Dictionary<string, string> { ["name"] = "John" });

        msg.Params.Should().ContainKey("name");
        msg.Params["name"].Should().Be("John");
    }

    [Fact]
    public void Create_WithoutLockeyPrefix_ShouldThrow()
    {
        var act = () => new LocalizedMessage("invalid_key");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*lockey_*");
    }

    [Fact]
    public void Create_WithEmptyKey_ShouldThrow()
    {
        var act = () => new LocalizedMessage("");

        act.Should().Throw<ArgumentException>();
    }
}
