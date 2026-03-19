using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.SharedKernel.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldHaveIsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithMessage_ShouldSetMessage()
    {
        var msg = new LocalizedMessage("lockey_test_success");

        var result = Result.Success(msg);

        result.Message.Should().NotBeNull();
        result.Message!.Key.Should().Be("lockey_test_success");
    }

    [Fact]
    public void Failure_ShouldHaveIsFailureTrue()
    {
        var result = Result.Failure("lockey_test_error");

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Failure_WithParams_ShouldIncludeParams()
    {
        var result = Result.Failure("lockey_test_error",
            new Dictionary<string, string> { ["field"] = "name" });

        result.Error!.Message.Params.Should().ContainKey("field");
    }
}

public sealed class ResultOfTTests
{
    [Fact]
    public void Success_ShouldContainValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_WithMessage_ShouldSetMessage()
    {
        var result = Result<string>.Success("data",
            new LocalizedMessage("lockey_test_created"));

        result.Value.Should().Be("data");
        result.Message!.Key.Should().Be("lockey_test_created");
    }

    [Fact]
    public void Failure_ShouldHaveNullValue()
    {
        var result = Result<int>.Failure("lockey_test_error");

        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default(int));
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Failure_WithError_ShouldContainError()
    {
        var error = new Error(new LocalizedMessage("lockey_test_error"));

        var result = Result<string>.Failure(error);

        result.Error.Should().Be(error);
    }
}

public sealed class PagedResultTests
{
    [Fact]
    public void TotalPages_ShouldCalculateCorrectly()
    {
        var result = new PagedResult<int>
        {
            Items = [1, 2, 3],
            TotalCount = 25,
            Page = 1,
            PageSize = 10
        };

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void HasNextPage_WhenNotLastPage_ShouldBeTrue()
    {
        var result = new PagedResult<int>
        {
            Items = [1],
            TotalCount = 20,
            Page = 1,
            PageSize = 10
        };

        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOnSecondPage_ShouldBeTrue()
    {
        var result = new PagedResult<int>
        {
            Items = [1],
            TotalCount = 20,
            Page = 2,
            PageSize = 10
        };

        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void TotalPages_WhenPageSizeZero_ShouldBeZero()
    {
        var result = new PagedResult<int>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 0
        };

        result.TotalPages.Should().Be(0);
    }
}
