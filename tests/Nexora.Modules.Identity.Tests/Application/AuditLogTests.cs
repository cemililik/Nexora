using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class AuditLogTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly UserId _userId = UserId.New();

    public AuditLogTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task RecordAuditLog_ValidCommand_ShouldPersist()
    {
        var handler = new RecordAuditLogHandler(_dbContext, _tenantAccessor, NullLogger<RecordAuditLogHandler>.Instance);
        var command = new RecordAuditLogCommand(
            _userId.Value, "login", "192.168.1.1", "Mozilla/5.0", "{\"method\":\"password\"}");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var count = await _dbContext.AuditLogs.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task RecordAuditLog_ShouldStoreAllFields()
    {
        var handler = new RecordAuditLogHandler(_dbContext, _tenantAccessor, NullLogger<RecordAuditLogHandler>.Instance);
        await handler.Handle(new RecordAuditLogCommand(
            _userId.Value, "login", "10.0.0.1", "Chrome/120", "{\"mfa\":true}"), CancellationToken.None);

        var log = await _dbContext.AuditLogs.FirstAsync();
        log.Action.Should().Be("login");
        log.IpAddress.Should().Be("10.0.0.1");
        log.UserAgent.Should().Be("Chrome/120");
        log.Details.Should().Contain("mfa");
        log.TenantId.Should().Be(_tenantId);
        log.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetAuditLogs_ShouldReturnPagedResults()
    {
        await SeedLogs(5);

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor, NullLogger<GetAuditLogsHandler>.Instance);
        var result = await handler.Handle(new GetAuditLogsQuery(Page: 1, PageSize: 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAuditLogs_FilterByUserId_ShouldFilterCorrectly()
    {
        var otherUserId = UserId.New();
        _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, "login"));
        _dbContext.AuditLogs.Add(AuditLog.Create(otherUserId, _tenantId, "login"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor, NullLogger<GetAuditLogsHandler>.Instance);
        var result = await handler.Handle(
            new GetAuditLogsQuery(UserId: _userId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAuditLogs_FilterByAction_ShouldFilterCorrectly()
    {
        _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, "login"));
        _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, "logout"));
        _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, "login"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor, NullLogger<GetAuditLogsHandler>.Instance);
        var result = await handler.Handle(
            new GetAuditLogsQuery(Action: "login"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAuditLogs_OrderByTimestampDescending()
    {
        _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, "first"));
        await _dbContext.SaveChangesAsync();
        await Task.Delay(10);
        _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, "second"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor, NullLogger<GetAuditLogsHandler>.Instance);
        var result = await handler.Handle(new GetAuditLogsQuery(), CancellationToken.None);

        result.Value!.Items[0].Action.Should().Be("second");
        result.Value.Items[1].Action.Should().Be("first");
    }

    [Fact]
    public void AuditLog_Create_ShouldSetTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var log = AuditLog.Create(_userId, _tenantId, "test");
        var after = DateTimeOffset.UtcNow;

        log.Timestamp.Should().BeOnOrAfter(before);
        log.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void AuditLog_Create_ShouldGenerateId()
    {
        var log = AuditLog.Create(_userId, _tenantId, "test");
        log.Id.Value.Should().NotBeEmpty();
    }

    private async Task SeedLogs(int count)
    {
        for (var i = 0; i < count; i++)
            _dbContext.AuditLogs.Add(AuditLog.Create(_userId, _tenantId, $"action-{i}"));
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
