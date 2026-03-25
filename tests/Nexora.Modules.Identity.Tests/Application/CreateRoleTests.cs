using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateRoleTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public CreateRoleTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task CreateRole_WithValidCommand_ReturnsCreatedRole()
    {
        var handler = new CreateRoleHandler(_dbContext, _tenantAccessor, NullLogger<CreateRoleHandler>.Instance);
        var command = new CreateRoleCommand("Editor", "Can edit content", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Editor");
        result.Value.Description.Should().Be("Can edit content");
        result.Value.IsSystemRole.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRole_WithPermissions_AssignsPermissions()
    {
        // Seed a permission
        var permission = Permission.Create("crm", "contacts", "read");
        await _dbContext.Permissions.AddAsync(permission);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateRoleHandler(_dbContext, _tenantAccessor, NullLogger<CreateRoleHandler>.Instance);
        var command = new CreateRoleCommand("Viewer", null, [permission.Id.Value]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Permissions.Should().ContainSingle()
            .Which.Should().Be("crm.contacts.read");
    }

    [Fact]
    public async Task CreateRole_WithDuplicateName_ReturnsFailure()
    {
        var handler = new CreateRoleHandler(_dbContext, _tenantAccessor, NullLogger<CreateRoleHandler>.Instance);
        await handler.Handle(new CreateRoleCommand("Admin", null, null), CancellationToken.None);

        var result = await handler.Handle(
            new CreateRoleCommand("Admin", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_name_taken");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
