using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetRolesQueryTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetRolesQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(_tenantId.Value.ToString());

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WhenRoleExists_ShouldReturnRolesWithPermissions()
    {
        var permission = Permission.Create("crm", "contacts", "read");
        await _dbContext.Permissions.AddAsync(permission);

        var role = Role.Create(_tenantId, "Editor");
        role.AssignPermission(permission);
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new GetRolesHandler(_dbContext, _tenantAccessor, NullLogger<GetRolesHandler>.Instance);
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].Name.Should().Be("Editor");
        result.Value[0].Permissions.Should().Contain("crm.contacts.read");
    }

    [Fact]
    public async Task Handle_EmptyDb_ShouldReturnEmptyList()
    {
        var handler = new GetRolesHandler(_dbContext, _tenantAccessor, NullLogger<GetRolesHandler>.Instance);
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
