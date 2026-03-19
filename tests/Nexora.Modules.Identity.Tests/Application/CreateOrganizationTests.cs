using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateOrganizationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public CreateOrganizationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateOrganization()
    {
        var handler = new CreateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<CreateOrganizationHandler>.Instance);
        var command = new CreateOrganizationCommand("Acme School", "acme-school");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Acme School");
        result.Value.Slug.Should().Be("acme-school");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateSlug_ShouldReturnFailure()
    {
        var org = Organization.Create(_tenantId, "First", "taken-slug");
        await _dbContext.Organizations.AddAsync(org);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<CreateOrganizationHandler>.Instance);
        var command = new CreateOrganizationCommand("Second", "taken-slug");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_org_slug_taken");
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        var handler = new CreateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<CreateOrganizationHandler>.Instance);
        var command = new CreateOrganizationCommand("Persisted Org", "persisted");

        await handler.Handle(command, CancellationToken.None);

        var count = await _dbContext.Organizations.CountAsync();
        count.Should().Be(1);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
