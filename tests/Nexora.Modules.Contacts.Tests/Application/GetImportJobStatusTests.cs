using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Contacts.Application.Queries;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetImportJobStatusTests
{
    [Fact]
    public async Task Handle_NonExistentJob_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new GetImportJobStatusHandler(
            NullLogger<GetImportJobStatusHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetImportJobStatusQuery(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_job_not_found");
    }

    [Fact]
    public async Task Handle_DifferentJobIds_ShouldAllReturnNotFound()
    {
        // Arrange
        var handler = new GetImportJobStatusHandler(
            NullLogger<GetImportJobStatusHandler>.Instance);

        // Act
        var result1 = await handler.Handle(
            new GetImportJobStatusQuery(Guid.NewGuid()), CancellationToken.None);
        var result2 = await handler.Handle(
            new GetImportJobStatusQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result1.IsFailure.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
    }
}
