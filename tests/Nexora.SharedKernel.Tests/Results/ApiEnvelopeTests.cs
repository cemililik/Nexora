using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.SharedKernel.Tests.Results;

public sealed class ApiEnvelopeTests
{
    [Fact]
    public void Success_ShouldSetData()
    {
        var envelope = ApiEnvelope<string>.Success("hello");

        envelope.Data.Should().Be("hello");
        envelope.Message.Should().BeNull();
        envelope.Errors.Should().BeNull();
    }

    [Fact]
    public void Success_WithMessage_ShouldSetMessageKey()
    {
        var msg = new LocalizedMessage("lockey_test_success");
        var envelope = ApiEnvelope<int>.Success(42, msg);

        envelope.Data.Should().Be(42);
        envelope.Message.Should().Be("lockey_test_success");
    }

    [Fact]
    public void Fail_ShouldSetMessageFromError()
    {
        var error = new Error(new LocalizedMessage("lockey_test_fail"));
        var envelope = ApiEnvelope<string>.Fail(error);

        envelope.Data.Should().BeNull();
        envelope.Message.Should().Be("lockey_test_fail");
    }

    [Fact]
    public void Fail_WithParams_ShouldSetMeta()
    {
        var error = new Error(new LocalizedMessage("lockey_test_fail",
            new Dictionary<string, string> { ["field"] = "name" }));

        var envelope = ApiEnvelope<string>.Fail(error);

        envelope.Meta.Should().ContainKey("field");
        envelope.Meta!["field"].Should().Be("name");
    }

    [Fact]
    public void Fail_WithDetails_ShouldSetErrors()
    {
        var details = new List<Error>
        {
            new(new LocalizedMessage("lockey_err_1")),
            new(new LocalizedMessage("lockey_err_2"))
        };
        var error = new Error(new LocalizedMessage("lockey_main"), details);

        var envelope = ApiEnvelope<string>.Fail(error);

        envelope.Errors.Should().HaveCount(2);
        envelope.Errors![0].Key.Should().Be("lockey_err_1");
        envelope.Errors[1].Key.Should().Be("lockey_err_2");
    }

    [Fact]
    public void ValidationFail_ShouldSetErrorsAndMessage()
    {
        var errors = new List<ApiValidationError>
        {
            new("lockey_val_1"),
            new("lockey_val_2", new Dictionary<string, string> { ["max"] = "100" })
        };

        var envelope = ApiEnvelope<string>.ValidationFail(errors);

        envelope.Message.Should().Be("lockey_validation_failed");
        envelope.Errors.Should().HaveCount(2);
        envelope.Errors![1].Params!["max"].Should().Be("100");
    }
}
