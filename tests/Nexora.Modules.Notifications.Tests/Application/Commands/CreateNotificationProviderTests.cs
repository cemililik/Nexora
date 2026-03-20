using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class CreateNotificationProviderTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateNotificationProviderTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidProvider_ShouldCreateProvider()
    {
        // Arrange
        var handler = new CreateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationProviderHandler>.Instance);
        var command = new CreateNotificationProviderCommand("Email", "SendGrid", "{\"apiKey\":\"test\"}", 1000, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Channel.Should().Be("Email");
        result.Value.ProviderName.Should().Be("SendGrid");
        result.Value.IsDefault.Should().BeTrue();
        result.Value.DailyLimit.Should().Be(1000);
    }

    [Fact]
    public async Task Handle_DuplicateProvider_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CreateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationProviderHandler>.Instance);
        var command = new CreateNotificationProviderCommand("Email", "SendGrid", "{}", 500);

        await handler.Handle(command, CancellationToken.None);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_provider_already_exists");
    }

    [Fact]
    public async Task Handle_SameProviderDifferentChannel_ShouldCreateBoth()
    {
        // Arrange
        var handler = new CreateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationProviderHandler>.Instance);

        await handler.Handle(new CreateNotificationProviderCommand("Email", "SendGrid", "{}", 500), CancellationToken.None);

        // Act — Twilio for SMS is a different channel
        var result = await handler.Handle(
            new CreateNotificationProviderCommand("Sms", "Twilio", "{}", 200), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var count = await _dbContext.NotificationProviders.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var handler = new CreateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationProviderHandler>.Instance);
        var command = new CreateNotificationProviderCommand("Email", "Mailgun", "{}", 1000);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var provider = await _dbContext.NotificationProviders.FirstAsync();
        provider.TenantId.Should().Be(_tenantId);
        provider.IsActive.Should().BeTrue();
        provider.SentToday.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NonDefaultProvider_ShouldSetIsDefaultFalse()
    {
        // Arrange
        var handler = new CreateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationProviderHandler>.Instance);
        var command = new CreateNotificationProviderCommand("Email", "SendGrid", "{}", 500);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefault.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
