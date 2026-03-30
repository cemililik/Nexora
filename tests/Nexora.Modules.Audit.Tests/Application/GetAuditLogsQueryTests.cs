using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class GetAuditLogsQueryTests
{
    private readonly IAuditEntryRepository _repository;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public GetAuditLogsQueryTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _repository = Substitute.For<IAuditEntryRepository>();
    }

    [Fact]
    public async Task Handle_NoEntries_ShouldReturnEmptyPage()
    {
        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<AuditEntry>() as IReadOnlyList<AuditEntry>, 0));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
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
        var entries = CreateEntries(10, module: "Contacts", operation: "CreateContact");

        _repository.GetPagedAsync(
                _tenantId, 1, 10, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries, 25));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
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
        var entries = CreateEntries(5, module: "Contacts", operation: "CreateContact");

        _repository.GetPagedAsync(
                _tenantId, 3, 10, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries, 25));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
        var query = new GetAuditLogsQuery(Page: 3, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(5);
        result.Value.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task Handle_FilterByModule_ShouldReturnOnlyMatchingModule()
    {
        var entries = CreateEntries(2, module: "Contacts", operation: "CreateContact");

        _repository.GetPagedAsync(
                _tenantId, 1, 20, "Contacts", null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries, 2));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
        var query = new GetAuditLogsQuery(Module: "Contacts");

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(i => i.Module == "Contacts");
    }

    [Fact]
    public async Task Handle_FilterByOperation_ShouldReturnOnlyMatchingOperation()
    {
        var entries = CreateEntries(2, module: "Contacts", operation: "CreateContact");

        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, "CreateContact", null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries, 2));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
        var query = new GetAuditLogsQuery(Operation: "CreateContact");

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(i => i.Operation == "CreateContact");
    }

    [Fact]
    public async Task Handle_FilterByIsSuccess_ShouldReturnOnlyMatchingStatus()
    {
        var entries = new List<AuditEntry>
        {
            CreateEntry(isSuccess: false)
        };

        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, null, null, null, false, null, null, Arg.Any<CancellationToken>())
            .Returns((entries as IReadOnlyList<AuditEntry>, 1));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
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
        var entries = CreateEntries(2, timestamp: now.AddDays(-2));

        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, null, null, null, null,
                Arg.Is<DateTimeOffset?>(d => d != null), Arg.Is<DateTimeOffset?>(d => d != null),
                Arg.Any<CancellationToken>())
            .Returns((entries, 2));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
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
        var entries = CreateEntries(2, userId: targetUserId);

        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, null, targetUserId, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries, 2));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
        var query = new GetAuditLogsQuery(UserId: targetUserId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldOrderByTimestampDescending()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<AuditEntry>
        {
            CreateEntry(timestamp: now),
            CreateEntry(timestamp: now.AddMinutes(-15)),
            CreateEntry(timestamp: now.AddMinutes(-30))
        };

        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries as IReadOnlyList<AuditEntry>, 3));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
        var query = new GetAuditLogsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeInDescendingOrder(i => i.Timestamp);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldNotReturnOtherTenantEntries()
    {
        var entries = new List<AuditEntry>
        {
            CreateEntry(module: "Contacts")
        };

        _repository.GetPagedAsync(
                _tenantId, 1, 20, null, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries as IReadOnlyList<AuditEntry>, 1));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
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
        var entries = new List<AuditEntry>
        {
            CreateEntry(module: "Contacts", operation: "CreateContact", userId: userId, isSuccess: true, timestamp: now)
        };

        _repository.GetPagedAsync(
                _tenantId, 1, 20, "Contacts", "CreateContact", userId, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries as IReadOnlyList<AuditEntry>, 1));

        var handler = new GetAuditLogsHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogsHandler>.Instance);
        var query = new GetAuditLogsQuery(
            Module: "Contacts",
            Operation: "CreateContact",
            UserId: userId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    private AuditEntry CreateEntry(
        string module = "Contacts",
        string operation = "CreateContact",
        Guid? userId = null,
        bool isSuccess = true,
        DateTimeOffset? timestamp = null)
    {
        return AuditEntry.Create(
            _tenantId, module, operation, "Command",
            userId ?? Guid.NewGuid(), "user@test.com", "127.0.0.1", null, null,
            isSuccess, null, null, null, null, null, null, null,
            timestamp ?? DateTimeOffset.UtcNow);
    }

    private IReadOnlyList<AuditEntry> CreateEntries(
        int count,
        string module = "Contacts",
        string operation = "CreateContact",
        Guid? userId = null,
        DateTimeOffset? timestamp = null)
    {
        var entries = new List<AuditEntry>();
        for (var i = 0; i < count; i++)
        {
            entries.Add(CreateEntry(module, operation, userId, true,
                timestamp ?? DateTimeOffset.UtcNow.AddMinutes(-i)));
        }
        return entries;
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
