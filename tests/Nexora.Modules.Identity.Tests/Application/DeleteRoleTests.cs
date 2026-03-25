using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class DeleteRoleTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public DeleteRoleTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task DeleteRole_WithValidRole_DeletesAndReturnsSuccess()
    {
        var role = Role.Create(_tenantId, "Deletable", "Will be deleted");
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteRoleHandler(_dbContext, _tenantAccessor, NullLogger<DeleteRoleHandler>.Instance);
        var command = new DeleteRoleCommand(role.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRole_WithSystemRole_ReturnsFailure()
    {
        var role = Role.Create(_tenantId, "Admin", null, isSystem: true);
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteRoleHandler(_dbContext, _tenantAccessor, NullLogger<DeleteRoleHandler>.Instance);
        var command = new DeleteRoleCommand(role.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_system_role_immutable");
    }

    [Fact]
    public async Task DeleteRole_WithRoleAssignedToUsers_ReturnsFailure()
    {
        var role = Role.Create(_tenantId, "InUse", "Has users");
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var orgUserId = OrganizationUserId.New();
        _dbContext.UserRoles.Add(UserRole.Create(orgUserId, role.Id));
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteRoleHandler(_dbContext, _tenantAccessor, NullLogger<DeleteRoleHandler>.Instance);
        var command = new DeleteRoleCommand(role.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_has_users");
    }

    [Fact]
    public async Task DeleteRole_WithNonExistentRole_ReturnsFailure()
    {
        var handler = new DeleteRoleHandler(_dbContext, _tenantAccessor, NullLogger<DeleteRoleHandler>.Instance);
        var command = new DeleteRoleCommand(Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_not_found");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
