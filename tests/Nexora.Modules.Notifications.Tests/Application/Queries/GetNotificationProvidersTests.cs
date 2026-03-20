using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Queries;

public sealed class GetNotificationProvidersTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetNotificationProvidersTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        var handler = new GetNotificationProvidersHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationProvidersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithProviders_ShouldReturnAll()
    {
        // Arrange
        await SeedProvider(NotificationChannel.Email, ProviderName.SendGrid);
        await SeedProvider(NotificationChannel.Sms, ProviderName.Twilio);
        var handler = new GetNotificationProvidersHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationProvidersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByChannel_ShouldFilterCorrectly()
    {
        // Arrange
        await SeedProvider(NotificationChannel.Email, ProviderName.SendGrid);
        await SeedProvider(NotificationChannel.Sms, ProviderName.Twilio);
        var handler = new GetNotificationProvidersHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationProvidersQuery(Channel: "Email"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Channel.Should().Be("Email");
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentTenant()
    {
        // Arrange
        await SeedProvider(NotificationChannel.Email, ProviderName.SendGrid);
        var otherProvider = NotificationProvider.Create(
            Guid.NewGuid(), NotificationChannel.Email, ProviderName.Mailgun, "{}", 500);
        await _dbContext.NotificationProviders.AddAsync(otherProvider);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationProvidersHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationProvidersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    private async Task SeedProvider(NotificationChannel channel, ProviderName name)
    {
        var provider = NotificationProvider.Create(_tenantId, channel, name, "{}", 1000);
        await _dbContext.NotificationProviders.AddAsync(provider);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
