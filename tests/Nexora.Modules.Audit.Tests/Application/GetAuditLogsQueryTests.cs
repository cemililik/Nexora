using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class GetAuditLogsQueryTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public GetAuditLogsQueryTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NoEntries_ShouldReturnEmptyPage()
    {
        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WithEntries_ShouldReturnPaginatedResults()
    {
        // Seed 25 entries
        for (var i = 0; i < 25; i++)
        {
            SeedAuditEntry(module: "Contacts", operation: "CreateContact",
                timestamp: DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(Page: 1, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_SecondPage_ShouldReturnCorrectItems()
    {
        for (var i = 0; i < 25; i++)
        {
            SeedAuditEntry(module: "Contacts", operation: "CreateContact",
                timestamp: DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(Page: 3, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(5);
        result.Value.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task Handle_FilterByModule_ShouldReturnOnlyMatchingModule()
    {
        SeedAuditEntry(module: "Contacts", operation: "CreateContact");
        SeedAuditEntry(module: "CRM", operation: "UpdateLead");
        SeedAuditEntry(module: "Contacts", operation: "DeleteContact");

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(Module: "Contacts");

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(i => i.Module == "Contacts");
    }

    [Fact]
    public async Task Handle_FilterByOperation_ShouldReturnOnlyMatchingOperation()
    {
        SeedAuditEntry(module: "Contacts", operation: "CreateContact");
        SeedAuditEntry(module: "Contacts", operation: "DeleteContact");
        SeedAuditEntry(module: "CRM", operation: "CreateContact");

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(Operation: "CreateContact");

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(i => i.Operation == "CreateContact");
    }

    [Fact]
    public async Task Handle_FilterByIsSuccess_ShouldReturnOnlyMatchingStatus()
    {
        SeedAuditEntry(isSuccess: true);
        SeedAuditEntry(isSuccess: true);
        SeedAuditEntry(isSuccess: false);

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(IsSuccess: false);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.Should().OnlyContain(i => !i.IsSuccess);
    }

    [Fact]
    public async Task Handle_FilterByDateRange_ShouldReturnOnlyInRange()
    {
        var now = DateTimeOffset.UtcNow;
        SeedAuditEntry(timestamp: now.AddDays(-10)); // outside range
        SeedAuditEntry(timestamp: now.AddDays(-3));   // in range
        SeedAuditEntry(timestamp: now.AddDays(-1));   // in range
        SeedAuditEntry(timestamp: now.AddDays(1));    // outside range

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(
            DateFrom: now.AddDays(-5),
            DateTo: now);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByUserId_ShouldReturnOnlyMatchingUser()
    {
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        SeedAuditEntry(userId: targetUserId);
        SeedAuditEntry(userId: otherUserId);
        SeedAuditEntry(userId: targetUserId);

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(UserId: targetUserId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldOrderByTimestampDescending()
    {
        var now = DateTimeOffset.UtcNow;
        SeedAuditEntry(timestamp: now.AddMinutes(-30));
        SeedAuditEntry(timestamp: now);
        SeedAuditEntry(timestamp: now.AddMinutes(-15));

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeInDescendingOrder(i => i.Timestamp);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldNotReturnOtherTenantEntries()
    {
        // Seed an entry for the current tenant
        SeedAuditEntry(module: "Contacts");

        // Seed an entry for a different tenant directly
        var entry = AuditEntry.Create(
            "other-tenant", "CRM", "UpdateLead", "Command",
            null, null, null, null, null, true, null, null, null,
            null, null, null, null, DateTimeOffset.UtcNow);
        _dbContext.AuditEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Module.Should().Be("Contacts");
    }

    [Fact]
    public async Task Handle_CombinedFilters_ShouldApplyAll()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SeedAuditEntry(module: "Contacts", operation: "CreateContact", userId: userId, isSuccess: true, timestamp: now);
        SeedAuditEntry(module: "Contacts", operation: "DeleteContact", userId: userId, isSuccess: true, timestamp: now);
        SeedAuditEntry(module: "CRM", operation: "CreateContact", userId: userId, isSuccess: true, timestamp: now);
        SeedAuditEntry(module: "Contacts", operation: "CreateContact", userId: Guid.NewGuid(), isSuccess: true, timestamp: now);

        var handler = new GetAuditLogsHandler(_dbContext, _tenantAccessor);
        var query = new GetAuditLogsQuery(
            Module: "Contacts",
            Operation: "CreateContact",
            UserId: userId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    public void Dispose() => _dbContext.Dispose();

    private void SeedAuditEntry(
        string module = "Contacts",
        string operation = "CreateContact",
        Guid? userId = null,
        bool isSuccess = true,
        DateTimeOffset? timestamp = null)
    {
        var entry = AuditEntry.Create(
            _tenantId, module, operation, "Command",
            userId ?? Guid.NewGuid(), "user@test.com", "127.0.0.1", null, null,
            isSuccess, null, null, null, null, null, null, null,
            timestamp ?? DateTimeOffset.UtcNow);
        _dbContext.AuditEntries.Add(entry);
        _dbContext.SaveChanges();
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
