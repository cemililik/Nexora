using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class FolderAccessExpiryJobTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FolderId _folderId;

    public FolderAccessExpiryJobTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);

        // Seed a folder to use for folder access entries
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _userId);
        _folderId = folder.Id;
        _dbContext.Folders.Add(folder);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task ExecuteForTenant_ExpiredAccesses_AreSoftDeleted()
    {
        // Arrange — create accesses that expired in the past
        var expired1 = FolderAccess.Create(_folderId, _userId, null, AccessPermission.View,
            DateTimeOffset.UtcNow.AddDays(-1));
        var expired2 = FolderAccess.Create(_folderId, null, Guid.NewGuid(), AccessPermission.Edit,
            DateTimeOffset.UtcNow.AddHours(-1));

        _dbContext.FolderAccesses.AddRange(expired1, expired2);
        await _dbContext.SaveChangesAsync();

        // Act
        var job = CreateJob();
        await RunJobForTenantAsync(job);

        // Assert — accesses should be soft-deleted (not visible without IgnoreQueryFilters)
        var remaining = await _dbContext.FolderAccesses.ToListAsync();
        remaining.Should().BeEmpty();

        var allIncludingDeleted = await _dbContext.FolderAccesses
            .IgnoreQueryFilters()
            .Where(a => a.Id == expired1.Id || a.Id == expired2.Id)
            .ToListAsync();
        allIncludingDeleted.Should().HaveCount(2);
        allIncludingDeleted.Should().AllSatisfy(a => a.IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task ExecuteForTenant_NonExpiredAccesses_AreNotAffected()
    {
        // Arrange — one future access and one without expiry
        var futureAccess = FolderAccess.Create(_folderId, _userId, null, AccessPermission.View,
            DateTimeOffset.UtcNow.AddDays(30));
        var permanentAccess = FolderAccess.Create(_folderId, null, Guid.NewGuid(), AccessPermission.Edit);

        _dbContext.FolderAccesses.AddRange(futureAccess, permanentAccess);
        await _dbContext.SaveChangesAsync();

        // Act
        var job = CreateJob();
        await RunJobForTenantAsync(job);

        // Assert — both accesses should still be visible
        var remaining = await _dbContext.FolderAccesses.ToListAsync();
        remaining.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteForTenant_NoExpiredAccesses_HandlesGracefully()
    {
        // Arrange — no folder accesses at all

        // Act
        var job = CreateJob();
        var act = () => RunJobForTenantAsync(job);

        // Assert — should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteForTenant_MixedAccesses_OnlyDeletesExpired()
    {
        // Arrange — mix of expired, future, and permanent accesses
        var expired = FolderAccess.Create(_folderId, _userId, null, AccessPermission.View,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var future = FolderAccess.Create(_folderId, null, Guid.NewGuid(), AccessPermission.Edit,
            DateTimeOffset.UtcNow.AddDays(7));
        var permanent = FolderAccess.Create(_folderId, Guid.NewGuid(), null, AccessPermission.View);

        _dbContext.FolderAccesses.AddRange(expired, future, permanent);
        await _dbContext.SaveChangesAsync();

        // Act
        var job = CreateJob();
        await RunJobForTenantAsync(job);

        // Assert — only the expired access should be gone
        var remaining = await _dbContext.FolderAccesses.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(a => a.Id == future.Id);
        remaining.Should().Contain(a => a.Id == permanent.Id);
    }

    public void Dispose() => _dbContext.Dispose();

    private FolderAccessExpiryJob CreateJob()
    {
        var tenantProvider = Substitute.For<IActiveTenantProvider>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var logger = NullLogger<FolderAccessExpiryJob>.Instance;

        return new FolderAccessExpiryJob(tenantProvider, scopeFactory, logger);
    }

    /// <summary>
    /// Invokes the protected ExecuteForTenantAsync via a testable wrapper.
    /// We create a service provider that returns our in-memory DbContext.
    /// </summary>
    private async Task RunJobForTenantAsync(FolderAccessExpiryJob job)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        var sp = services.BuildServiceProvider();

        var tenant = new ActiveTenantInfo(_tenantId.ToString(), $"tenant_{_tenantId}");

        // Use reflection to call the protected method since it's the cleanest
        // approach without modifying the production code's visibility
        var method = typeof(FolderAccessExpiryJob).GetMethod(
            "ExecuteForTenantAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(job, [
            new FolderAccessExpiryJobParams { TenantId = tenant.TenantId }, tenant, sp, CancellationToken.None
        ])!;

        await task;
    }

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
