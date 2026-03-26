using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class DeleteUserTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly TenantId _tenantId = TenantId.New();
    private const string CurrentKeycloakUserId = "current-kc-user-id";

    private readonly ITenantContextAccessor _tenantAccessor;

    public DeleteUserTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.Value.ToString(), null, CurrentKeycloakUserId);
        _tenantAccessor = accessor;

        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _platformDb = new PlatformDbContext(platformOptions);

        var tenant = Tenant.Create("Test", "test");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.SetRealmId("tenant-test");
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    [Fact]
    public async Task DeleteUser_WithValidUser_DeletesAndDisablesInKeycloak()
    {
        var user = User.Create(_tenantId, "other-kc-id", "user@test.com", "Test", "User");
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(user.Id, orgId);
        _dbContext.Users.Add(user);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteUserHandler(
            _dbContext, _platformDb, _tenantAccessor, _keycloakAdmin,
            NullLogger<DeleteUserHandler>.Instance);

        var result = await handler.Handle(
            new DeleteUserCommand(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keycloakAdmin.Received(1).DisableUserAsync(
            "tenant-test", "other-kc-id", Arg.Any<CancellationToken>());

        var deletedUser = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        deletedUser!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUser_WithSelfDeletion_ReturnsFailure()
    {
        var user = User.Create(_tenantId, CurrentKeycloakUserId, "self@test.com", "Self", "User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteUserHandler(
            _dbContext, _platformDb, _tenantAccessor, _keycloakAdmin,
            NullLogger<DeleteUserHandler>.Instance);

        var result = await handler.Handle(
            new DeleteUserCommand(user.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_cannot_delete_self");
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentUser_ReturnsFailure()
    {
        var handler = new DeleteUserHandler(
            _dbContext, _platformDb, _tenantAccessor, _keycloakAdmin,
            NullLogger<DeleteUserHandler>.Instance);

        var result = await handler.Handle(
            new DeleteUserCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact]
    public async Task DeleteUser_WithKeycloakUnavailable_DeletesAnyway()
    {
        var user = User.Create(_tenantId, "kc-unavail", "unavail@test.com", "Un", "Avail");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _keycloakAdmin
            .DisableUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Keycloak is down"));

        var handler = new DeleteUserHandler(
            _dbContext, _platformDb, _tenantAccessor, _keycloakAdmin,
            NullLogger<DeleteUserHandler>.Instance);

        var result = await handler.Handle(
            new DeleteUserCommand(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var deletedUser = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        deletedUser!.IsDeleted.Should().BeTrue();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _platformDb.Dispose();
    }
}
