using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Audit.Infrastructure.Stores;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Testcontainers.PostgreSql;
using AuditEntryRecord = Nexora.SharedKernel.Abstractions.Audit.AuditEntry;

namespace Nexora.Modules.Audit.Tests.Infrastructure;

public sealed class PostgresAuditStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private AuditDbContext _dbContext = null!;
    private string _tenantId = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _tenantId = Guid.NewGuid().ToString();
        var tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new AuditDbContext(options, tenantAccessor);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _dbContext.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task SaveAsync_ValidEntry_ShouldPersistToDatabase()
    {
        var store = new PostgresAuditStore(_dbContext, NullLogger<PostgresAuditStore>.Instance);

        var userId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var entry = new AuditEntryRecord(
            Id: Guid.NewGuid(),
            TenantId: _tenantId,
            Module: "Contacts",
            Operation: "CreateContact",
            OperationType: OperationType.Action,
            UserId: userId,
            UserEmail: "user@test.com",
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0",
            CorrelationId: "corr-123",
            IsSuccess: true,
            ErrorKey: null,
            EntityType: "Contact",
            EntityId: "entity-1",
            BeforeState: null,
            AfterState: "{\"name\":\"John\"}",
            Changes: "{\"name\":[null,\"John\"]}",
            Metadata: "{\"source\":\"api\"}",
            Timestamp: timestamp);

        await store.SaveAsync(entry, CancellationToken.None);

        var count = await _dbContext.AuditEntries.CountAsync();
        count.Should().Be(1);

        var persisted = await _dbContext.AuditEntries.FirstAsync();
        persisted.TenantId.Should().Be(_tenantId);
        persisted.Module.Should().Be("Contacts");
        persisted.Operation.Should().Be("CreateContact");
        persisted.OperationType.Should().Be("Action");
        persisted.UserId.Should().Be(userId);
        persisted.UserEmail.Should().Be("user@test.com");
        persisted.IpAddress.Should().Be("192.168.1.1");
        persisted.UserAgent.Should().Be("Mozilla/5.0");
        persisted.CorrelationId.Should().Be("corr-123");
        persisted.IsSuccess.Should().BeTrue();
        persisted.ErrorKey.Should().BeNull();
        persisted.EntityType.Should().Be("Contact");
        persisted.EntityId.Should().Be("entity-1");
        persisted.BeforeState.Should().BeNull();
        persisted.AfterState.Should().Be("{\"name\":\"John\"}");
        persisted.Changes.Should().Be("{\"name\":[null,\"John\"]}");
        persisted.Metadata.Should().Be("{\"source\":\"api\"}");
        persisted.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task SaveAsync_FailedEntry_ShouldPersistErrorKey()
    {
        var store = new PostgresAuditStore(_dbContext, NullLogger<PostgresAuditStore>.Instance);

        var entry = new AuditEntryRecord(
            Id: Guid.NewGuid(),
            TenantId: _tenantId,
            Module: "Identity",
            Operation: "Login",
            OperationType: OperationType.Action,
            UserId: null,
            UserEmail: "attacker@test.com",
            IpAddress: "10.0.0.1",
            UserAgent: null,
            CorrelationId: null,
            IsSuccess: false,
            ErrorKey: "lockey_identity_error_invalid_credentials",
            EntityType: null,
            EntityId: null,
            BeforeState: null,
            AfterState: null,
            Changes: null,
            Metadata: null,
            Timestamp: DateTimeOffset.UtcNow);

        await store.SaveAsync(entry, CancellationToken.None);

        var persisted = await _dbContext.AuditEntries.FirstAsync();
        persisted.IsSuccess.Should().BeFalse();
        persisted.ErrorKey.Should().Be("lockey_identity_error_invalid_credentials");
        persisted.UserId.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_MultipleEntries_ShouldPersistAll()
    {
        var store = new PostgresAuditStore(_dbContext, NullLogger<PostgresAuditStore>.Instance);

        for (var i = 0; i < 5; i++)
        {
            var entry = new AuditEntryRecord(
                Id: Guid.NewGuid(),
                TenantId: _tenantId,
                Module: "Contacts",
                Operation: $"Op{i}",
                OperationType: OperationType.Action,
                UserId: null, UserEmail: null, IpAddress: null, UserAgent: null,
                CorrelationId: null, IsSuccess: true, ErrorKey: null,
                EntityType: null, EntityId: null, BeforeState: null, AfterState: null,
                Changes: null, Metadata: null, Timestamp: DateTimeOffset.UtcNow);

            await store.SaveAsync(entry, CancellationToken.None);
        }

        var count = await _dbContext.AuditEntries.CountAsync();
        count.Should().Be(5);
    }

    [Fact]
    public async Task SaveAsync_ReadOperationType_MapsToStringRepresentation()
    {
        var store = new PostgresAuditStore(_dbContext, NullLogger<PostgresAuditStore>.Instance);

        var entry = new AuditEntryRecord(
            Id: Guid.NewGuid(),
            TenantId: _tenantId,
            Module: "Contacts",
            Operation: "GetContacts",
            OperationType: OperationType.Read,
            UserId: null, UserEmail: null, IpAddress: null, UserAgent: null,
            CorrelationId: null, IsSuccess: true, ErrorKey: null,
            EntityType: null, EntityId: null, BeforeState: null, AfterState: null,
            Changes: null, Metadata: null, Timestamp: DateTimeOffset.UtcNow);

        await store.SaveAsync(entry, CancellationToken.None);

        var persisted = await _dbContext.AuditEntries.FirstAsync();
        persisted.OperationType.Should().Be("Read");
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
