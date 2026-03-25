using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class UpdateTenantStatusTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;

    public UpdateTenantStatusTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _platformDb = new PlatformDbContext(options);
    }

    [Fact]
    public async Task UpdateTenantStatus_WithActivateAction_ChangesStatusToActive()
    {
        var tenant = Tenant.Create("Test", "test");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new UpdateTenantStatusHandler(_platformDb, NullLogger<UpdateTenantStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTenantStatusCommand(tenant.Id.Value, "activate"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _platformDb.Tenants.FirstAsync();
        updated.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task UpdateTenantStatus_WithSuspendAction_ChangesStatusToSuspended()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new UpdateTenantStatusHandler(_platformDb, NullLogger<UpdateTenantStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTenantStatusCommand(tenant.Id.Value, "suspend"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _platformDb.Tenants.FirstAsync();
        updated.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public async Task UpdateTenantStatus_WithTerminateAction_ChangesStatusToTerminated()
    {
        var tenant = Tenant.Create("Test", "test");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new UpdateTenantStatusHandler(_platformDb, NullLogger<UpdateTenantStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTenantStatusCommand(tenant.Id.Value, "terminate"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _platformDb.Tenants.FirstAsync();
        updated.Status.Should().Be(TenantStatus.Terminated);
    }

    [Fact]
    public async Task UpdateTenantStatus_WithNonExistentTenant_ReturnsFailure()
    {
        var handler = new UpdateTenantStatusHandler(_platformDb, NullLogger<UpdateTenantStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTenantStatusCommand(Guid.NewGuid(), "activate"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_not_found");
    }

    public void Dispose() => _platformDb.Dispose();
}
