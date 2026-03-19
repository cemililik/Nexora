using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactExportTests
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public StartContactExportTests()
    {
        _tenantAccessor = CreateTenantAccessor(Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnQueuedJob()
    {
        // Arrange
        var handler = new StartContactExportHandler(
            _tenantAccessor, NullLogger<StartContactExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactExportCommand("csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.Format.Should().Be("csv");
    }

    [Fact]
    public async Task Handle_FormatUpperCase_ShouldNormalizeToLowerCase()
    {
        // Arrange
        var handler = new StartContactExportHandler(
            _tenantAccessor, NullLogger<StartContactExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactExportCommand("JSON"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Format.Should().Be("json");
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnUniqueJobId()
    {
        // Arrange
        var handler = new StartContactExportHandler(
            _tenantAccessor, NullLogger<StartContactExportHandler>.Instance);

        // Act
        var result1 = await handler.Handle(
            new StartContactExportCommand("csv"), CancellationToken.None);
        var result2 = await handler.Handle(
            new StartContactExportCommand("csv"), CancellationToken.None);

        // Assert
        result1.Value!.JobId.Should().NotBe(result2.Value!.JobId);
    }

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
