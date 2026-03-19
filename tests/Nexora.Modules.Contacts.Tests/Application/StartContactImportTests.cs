using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactImportTests
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public StartContactImportTests()
    {
        _tenantAccessor = CreateTenantAccessor(Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnQueuedJob()
    {
        // Arrange
        var handler = new StartContactImportHandler(
            _tenantAccessor, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.JobId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnCorrectTimestamp()
    {
        // Arrange
        var handler = new StartContactImportHandler(
            _tenantAccessor, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var before = DateTimeOffset.UtcNow;
        var result = await handler.Handle(
            new StartContactImportCommand("data.xlsx", "xlsx", new byte[] { 1 }),
            CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnZeroCounts()
    {
        // Arrange
        var handler = new StartContactImportHandler(
            _tenantAccessor, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", new byte[] { 1 }),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRows.Should().Be(0);
        result.Value.ProcessedRows.Should().Be(0);
        result.Value.SuccessCount.Should().Be(0);
        result.Value.ErrorCount.Should().Be(0);
    }

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
