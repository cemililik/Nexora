using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class UpdateOrganizationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public UpdateOrganizationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldUpdateOrganization()
    {
        var org = Organization.Create(_tenantId, "Old Name", "old-slug");
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<UpdateOrganizationHandler>.Instance);
        var command = new UpdateOrganizationCommand(org.Id.Value, "New Name", "Europe/Istanbul", "TRY", "tr");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Name");
        result.Value.Timezone.Should().Be("Europe/Istanbul");
        result.Value.DefaultCurrency.Should().Be("TRY");
        result.Value.DefaultLanguage.Should().Be("tr");
    }

    [Fact]
    public async Task Handle_NonExistentOrg_ShouldReturnFailure()
    {
        var handler = new UpdateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<UpdateOrganizationHandler>.Instance);
        var command = new UpdateOrganizationCommand(Guid.NewGuid(), "Name", "UTC", "USD", "en");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_org_not_found");
    }

    [Fact]
    public async Task Handle_OtherTenantOrg_ShouldReturnFailure()
    {
        var otherTenantId = TenantId.New();
        var org = Organization.Create(otherTenantId, "Other", "other");
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<UpdateOrganizationHandler>.Instance);
        var command = new UpdateOrganizationCommand(org.Id.Value, "Hijack", "UTC", "USD", "en");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
