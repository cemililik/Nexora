using System.Diagnostics;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Api.ContractTests;

/// <summary>
/// Verifies the structural contract of the ApiEnvelope response wrapper.
/// These tests ensure that Success, Fail, and ValidationFail factory methods
/// produce envelopes with the expected shape, preventing regressions in the API contract.
/// </summary>
public sealed class ApiEnvelopeContractTests
{
    [Fact]
    public void Success_ShouldIncludeDataAndMessage()
    {
        // Arrange
        var data = new { Id = Guid.NewGuid(), Name = "Test" };
        var message = LocalizedMessage.Of("lockey_test_success");

        // Act
        var envelope = ApiEnvelope<object>.Success(data, message);

        // Assert
        envelope.Data.Should().BeSameAs(data);
        envelope.Message.Should().Be("lockey_test_success");
        envelope.Errors.Should().BeNull();
        envelope.Meta.Should().BeNull();
    }

    [Fact]
    public void Success_WithoutMessage_ShouldHaveNullMessage()
    {
        // Arrange
        var data = new { Id = 1 };

        // Act
        var envelope = ApiEnvelope<object>.Success(data);

        // Assert
        envelope.Data.Should().BeSameAs(data);
        envelope.Message.Should().BeNull();
        envelope.Errors.Should().BeNull();
    }

    [Fact]
    public void Fail_ShouldIncludeErrorAndTraceId()
    {
        // Arrange
        var error = new Error(
            LocalizedMessage.Of("lockey_error_not_found"),
            Details: null);
        var traceId = "abc123";

        // Act
        var envelope = ApiEnvelope<object>.Fail(error, traceId);

        // Assert
        envelope.Data.Should().BeNull();
        envelope.Message.Should().Be("lockey_error_not_found");
        envelope.TraceId.Should().Be("abc123");
        envelope.Errors.Should().BeNull();
    }

    [Fact]
    public void Fail_WithDetails_ShouldIncludeValidationErrors()
    {
        // Arrange
        var details = new List<Error>
        {
            new(LocalizedMessage.Of("lockey_field_required", new() { ["field"] = "email" })),
            new(LocalizedMessage.Of("lockey_field_too_long", new() { ["field"] = "name", ["max"] = "100" }))
        };
        var error = new Error(
            LocalizedMessage.Of("lockey_validation_failed"),
            details);

        // Act
        var envelope = ApiEnvelope<object>.Fail(error);

        // Assert
        envelope.Message.Should().Be("lockey_validation_failed");
        envelope.Errors.Should().HaveCount(2);
        envelope.Errors![0].Key.Should().Be("lockey_field_required");
        envelope.Errors[0].Params.Should().ContainKey("field").WhoseValue.Should().Be("email");
        envelope.Errors[1].Key.Should().Be("lockey_field_too_long");
        envelope.Errors[1].Params.Should().ContainKey("max").WhoseValue.Should().Be("100");
    }

    [Fact]
    public void Fail_WithParams_ShouldIncludeMeta()
    {
        // Arrange
        var error = new Error(
            LocalizedMessage.Of("lockey_error_limit_exceeded", new() { ["limit"] = "50" }));

        // Act
        var envelope = ApiEnvelope<object>.Fail(error);

        // Assert
        envelope.Meta.Should().NotBeNull();
        envelope.Meta.Should().ContainKey("limit").WhoseValue.Should().Be("50");
    }

    [Fact]
    public void Fail_WithEmptyParams_ShouldHaveNullMeta()
    {
        // Arrange — LocalizedMessage with no params creates an empty dictionary
        var error = new Error(LocalizedMessage.Of("lockey_error_generic"));

        // Act
        var envelope = ApiEnvelope<object>.Fail(error);

        // Assert
        envelope.Meta.Should().BeNull("Meta should be null when error has no params");
    }

    [Fact]
    public void ValidationFail_ShouldIncludeValidationErrors()
    {
        // Arrange
        var errors = new List<ApiValidationError>
        {
            new("lockey_contacts_validation_email_format"),
            new("lockey_contacts_validation_type_required", new() { ["field"] = "Type" })
        };
        var traceId = "trace-456";

        // Act
        var envelope = ApiEnvelope<object>.ValidationFail(errors, traceId);

        // Assert
        envelope.Message.Should().Be("lockey_validation_failed");
        envelope.TraceId.Should().Be("trace-456");
        envelope.Data.Should().BeNull();
        envelope.Errors.Should().HaveCount(2);
        envelope.Errors![0].Key.Should().Be("lockey_contacts_validation_email_format");
        envelope.Errors[0].Params.Should().BeNull();
        envelope.Errors[1].Key.Should().Be("lockey_contacts_validation_type_required");
        envelope.Errors[1].Params.Should().ContainKey("field");
    }

    [Fact]
    public void Success_NullData_ShouldStillWrapInEnvelope()
    {
        // Arrange & Act
        var envelope = ApiEnvelope<string?>.Success(null);

        // Assert
        envelope.Data.Should().BeNull();
        envelope.Message.Should().BeNull();
        envelope.Errors.Should().BeNull();
        envelope.Meta.Should().BeNull();
        // The envelope itself is not null — only Data is null
    }

    [Fact]
    public void ApiValidationError_ShouldBeRecord()
    {
        // Verify ApiValidationError is a record by checking for the compiler-generated <Clone>$ method.
        // Note: record equality for Dictionary properties uses reference equality (Dictionary
        // does not override Equals), so we verify the record nature via reflection instead.
        var type = typeof(ApiValidationError);
        var cloneMethod = type.GetMethod("<Clone>$");

        cloneMethod.Should().NotBeNull("ApiValidationError should be a record (has <Clone>$ method)");

        // Additionally verify value equality works for the Key property (non-collection)
        var error1 = new ApiValidationError("lockey_test");
        var error2 = new ApiValidationError("lockey_test");
        error1.Should().Be(error2);
    }

    [Fact]
    public void ApiEnvelope_ShouldBeRecord()
    {
        // Verify ApiEnvelope is a sealed record (immutable contract).
        // C# records compile as classes with System.Object as the base type —
        // they do NOT inherit from a special record base class. The correct way
        // to verify a type is a record is to check for the compiler-generated <Clone>$ method.
        var envelopeType = typeof(ApiEnvelope<>);

        envelopeType.IsSealed.Should().BeTrue("ApiEnvelope should be sealed");

        var cloneMethod = envelopeType.GetMethod("<Clone>$");
        cloneMethod.Should().NotBeNull(
            "ApiEnvelope should be a record (has compiler-generated <Clone>$ method)");
    }
}
