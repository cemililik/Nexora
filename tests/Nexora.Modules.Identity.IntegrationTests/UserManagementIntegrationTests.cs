using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Nexora.Modules.Identity.IntegrationTests;

/// <summary>Integration tests for user management flows: create, query, update, and delete.</summary>
public sealed class UserManagementIntegrationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly TenantId _tenantId = TenantId.New();

    public UserManagementIntegrationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        _keycloakAdmin.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("kc-generated-id");

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _platformDb = new PlatformDbContext(platformOptions);

        SeedTenant(_tenantId, "Test Tenant", "test", "tenant-test");
    }

    [Fact]
    public async Task CreateUser_ThenGetById_ShouldReturnCreatedUser()
    {
        // Arrange
        var createHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateUserCommand("alice@example.com", "Alice", "Wonder", "TempPass1!"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var createdUserId = createResult.Value!.Id;

        // Act
        var queryHandler = new GetUserByIdHandler(_dbContext, _tenantAccessor);
        var queryResult = await queryHandler.Handle(
            new GetUserByIdQuery(createdUserId), CancellationToken.None);

        // Assert
        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value!.Email.Should().Be("alice@example.com");
        queryResult.Value.FirstName.Should().Be("Alice");
        queryResult.Value.LastName.Should().Be("Wonder");
        queryResult.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ShouldFail()
    {
        // Arrange
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        await handler.Handle(
            new CreateUserCommand("dup@example.com", "First", "User", "TempPass1!"), CancellationToken.None);

        // Act
        var result = await handler.Handle(
            new CreateUserCommand("dup@example.com", "Second", "User", "TempPass1!"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_email_taken");
    }

    [Fact]
    public async Task UpdateUserProfile_ShouldPersistChanges()
    {
        // Arrange: create a user first
        var createHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateUserCommand("update@example.com", "Original", "Name", "TempPass1!"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var userId = createResult.Value!.Id;

        // Act: update the profile
        var updateHandler = new UpdateUserProfileHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserProfileHandler>.Instance);
        var updateResult = await updateHandler.Handle(
            new UpdateUserProfileCommand(userId, "Updated", "Person", "+1234567890"), CancellationToken.None);

        // Assert: verify the update result
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.FirstName.Should().Be("Updated");
        updateResult.Value.LastName.Should().Be("Person");
        updateResult.Value.Phone.Should().Be("+1234567890");

        // Assert: re-query to confirm persistence
        var queryHandler = new GetUserByIdHandler(_dbContext, _tenantAccessor);
        var queryResult = await queryHandler.Handle(new GetUserByIdQuery(userId), CancellationToken.None);

        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value!.FirstName.Should().Be("Updated");
        queryResult.Value.LastName.Should().Be("Person");
        queryResult.Value.Phone.Should().Be("+1234567890");
    }

    [Fact]
    public async Task DeleteUser_ShouldSoftDelete()
    {
        // Arrange: create a user
        var createHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateUserCommand("delete@example.com", "Delete", "Me", "TempPass1!"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var userId = createResult.Value!.Id;

        // Use a different UserId for tenant context to avoid self-delete prevention
        var deleteAccessor = CreateTenantAccessor(_tenantId, userId: "different-kc-user");

        // Act: delete the user
        var deleteHandler = new DeleteUserHandler(_dbContext, _platformDb, deleteAccessor, _keycloakAdmin, NullLogger<DeleteUserHandler>.Instance);
        var deleteResult = await deleteHandler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        // Assert: deletion succeeded
        deleteResult.IsSuccess.Should().BeTrue();

        // Assert: user is soft-deleted (not visible through normal query due to global query filter)
        var userInDb = await _dbContext.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == UserId.From(userId));

        userInDb.Should().NotBeNull();
        userInDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUser_ThenQuery_ShouldNotReturn()
    {
        // Arrange: create a user
        var createHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateUserCommand("vanish@example.com", "Vanish", "User", "TempPass1!"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var userId = createResult.Value!.Id;

        // Use a different UserId for tenant context to avoid self-delete prevention
        var deleteAccessor = CreateTenantAccessor(_tenantId, userId: "different-kc-user");

        // Act: delete the user
        var deleteHandler = new DeleteUserHandler(_dbContext, _platformDb, deleteAccessor, _keycloakAdmin, NullLogger<DeleteUserHandler>.Instance);
        await deleteHandler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        // Assert: query by ID should fail (user not found)
        var queryHandler = new GetUserByIdHandler(_dbContext, _tenantAccessor);
        var queryResult = await queryHandler.Handle(new GetUserByIdQuery(userId), CancellationToken.None);

        queryResult.IsFailure.Should().BeTrue();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _platformDb.Dispose();
    }

    private void SeedTenant(TenantId tenantId, string name, string slug, string realmId)
    {
        var tenant = Tenant.Create(name, slug);
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, tenantId);
        tenant.SetRealmId(realmId);
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId, string? userId = null)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString(), userId: userId);
        return accessor;
    }
}
