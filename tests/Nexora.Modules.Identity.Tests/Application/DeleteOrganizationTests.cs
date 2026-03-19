using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class DeleteOrganizationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public DeleteOrganizationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldDeactivateOrganization()
    {
        var org = Organization.Create(_tenantId, "To Delete", "to-delete");
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<DeleteOrganizationHandler>.Instance);
        var result = await handler.Handle(new DeleteOrganizationCommand(org.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Organizations.FindAsync(org.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentOrg_ShouldReturnFailure()
    {
        var handler = new DeleteOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<DeleteOrganizationHandler>.Instance);
        var result = await handler.Handle(new DeleteOrganizationCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_org_not_found");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
