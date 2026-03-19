using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateUserTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public CreateUserTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateUser()
    {
        var handler = new CreateUserHandler(_dbContext, _tenantAccessor);
        var command = new CreateUserCommand("kc-123", "john@example.com", "John", "Doe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("john@example.com");
        result.Value.FirstName.Should().Be("John");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldReturnFailure()
    {
        var handler = new CreateUserHandler(_dbContext, _tenantAccessor);
        await handler.Handle(
            new CreateUserCommand("kc-1", "taken@example.com", "A", "B"), CancellationToken.None);

        var result = await handler.Handle(
            new CreateUserCommand("kc-2", "taken@example.com", "C", "D"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_email_taken");
    }

    [Fact]
    public async Task Handle_ShouldPersistUser()
    {
        var handler = new CreateUserHandler(_dbContext, _tenantAccessor);
        await handler.Handle(
            new CreateUserCommand("kc-1", "p@test.com", "P", "T"), CancellationToken.None);

        var count = await _dbContext.Users.CountAsync();
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
