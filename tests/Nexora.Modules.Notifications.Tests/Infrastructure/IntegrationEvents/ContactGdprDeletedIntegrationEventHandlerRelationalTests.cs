using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Nexora.Modules.Notifications.Tests.Infrastructure.IntegrationEvents;

/// <summary>
/// T-017 follow-up: covers the handler's <c>ExecuteUpdateAsync</c> branch (real
/// Postgres, bulk update path) which is unreachable from the InMemory provider.
/// Asserts <c>BodyRendered</c> ends up <c>null</c> (T-017 contract) and that
/// the scrub is idempotent across re-invocations.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ContactGdprDeletedIntegrationEventHandlerRelationalTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private NotificationsDbContext _dbContext = null!;
    private ITenantContextAccessor _tenantAccessor = null!;
    private readonly HashSet<Guid> _processedEventIds = [];
    private IInboxGuard _inboxGuard = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var schema = $"tenant_{_tenantId:D}";

        // Tenant schema must exist before EF model build assigns it as the
        // default. Pure DDL — identifier-safe, only this test creates the
        // value (Guid).
        await using (var setup = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await setup.OpenAsync();
            await using var cmd = setup.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _inboxGuard = Substitute.For<IInboxGuard>();
        _inboxGuard.IsAlreadyProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => _processedEventIds.Contains(ci.ArgAt<Guid>(0)));
        _inboxGuard.When(g => g.MarkAsProcessed(Arg.Any<Guid>(), Arg.Any<string>()))
            .Do(ci => _processedEventIds.Add(ci.ArgAt<Guid>(0)));

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);

        var creator = _dbContext.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task HandleAsync_RelationalProvider_NullsBodyRendered_AndIsIdempotent()
    {
        var contactId = Guid.NewGuid();

        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject containing PII",
            "Hi John, your invoice for $100 is ready.", "test");
        notification.AddRecipient(contactId, "john@example.com");
        notification.ClearDomainEvents();
        // Add (sync) not AddAsync: AddAsync's only async branch is for value
        // generators (rare); for plain entity attach, the sync overload is
        // semantically identical and avoids a misleading await.
        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        var handler = new ContactGdprDeletedIntegrationEventHandler(
            _dbContext, _inboxGuard,
            new Nexora.Infrastructure.Gdpr.NoOpGdprRenamedTableScanner<Nexora.Modules.Notifications.Infrastructure.NotificationsDbContext>(),
            NullLogger<ContactGdprDeletedIntegrationEventHandler>.Instance);

        var @event = new ContactGdprDeletedIntegrationEvent
        {
            // EventId + OccurredAt auto-populated by IntegrationEventBase.
            TenantId = _tenantId.ToString(),
            ContactId = contactId,
            Reason = "test",
            Mode = "hard_deleted",
            DeletedAtUtc = DateTime.UtcNow,
            ErasedByUserId = Guid.NewGuid()
        };

        // Act — first run hits the ExecuteUpdateAsync branch (relational).
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — body is null, subject is the placeholder, parity with the
        // InMemory test class.
        var afterFirst = await _dbContext.Notifications.AsNoTracking()
            .FirstAsync(n => n.Id == notification.Id);
        afterFirst.BodyRendered.Should().BeNull(
            "T-017: ExecuteUpdateAsync must produce the same final state as the in-memory ScrubRenderedBody() — null body, never a placeholder.");
        afterFirst.Subject.Should().Be(Nexora.SharedKernel.Constants.PiiRedactedPlaceholder.Value,
            "Subject column is NOT NULL; the placeholder is correct on both code paths.");

        // Act — re-run with the same event id (inbox-guarded short-circuit).
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — values unchanged; idempotent.
        var afterSecond = await _dbContext.Notifications.AsNoTracking()
            .FirstAsync(n => n.Id == notification.Id);
        afterSecond.BodyRendered.Should().BeNull("re-run must not flip null back to a placeholder.");
        afterSecond.Subject.Should().Be(Nexora.SharedKernel.Constants.PiiRedactedPlaceholder.Value);
    }
}
