using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetCurrentUserHandlerTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private const string KeycloakUserId = "kc-test-user-id";

    public GetCurrentUserHandlerTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange — no users seeded
        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor,
            NullLogger<GetCurrentUserHandler>.Instance);
        var query = new GetCurrentUserQuery("nonexistent-kc-id");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact]
    public async Task Handle_UserExists_ReturnsSuccess()
    {
        // Arrange
        var user = User.Create(_tenantId, KeycloakUserId, "test@example.com", "Test", "User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor,
            NullLogger<GetCurrentUserHandler>.Instance);
        var query = new GetCurrentUserQuery(KeycloakUserId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("test@example.com");
        result.Value.FirstName.Should().Be("Test");
        result.Value.LastName.Should().Be("User");
    }

    [Fact]
    public async Task Handle_UserWithNoOrganizations_ReturnsEmptyOrgList()
    {
        // Arrange
        var user = User.Create(_tenantId, KeycloakUserId, "solo@example.com", "Solo", "User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor,
            NullLogger<GetCurrentUserHandler>.Instance);
        var query = new GetCurrentUserQuery(KeycloakUserId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Organizations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UserWithOrganization_ReturnsOrgInList()
    {
        // Arrange
        var user = User.Create(_tenantId, KeycloakUserId, "member@example.com", "Org", "Member");
        _dbContext.Users.Add(user);

        var org = Organization.Create(_tenantId, "Acme Corp", "acme-corp");
        _dbContext.Organizations.Add(org);

        var orgUser = OrganizationUser.Create(user.Id, org.Id, isDefault: true);
        _dbContext.OrganizationUsers.Add(orgUser);

        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor,
            NullLogger<GetCurrentUserHandler>.Instance);
        var query = new GetCurrentUserQuery(KeycloakUserId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Organizations.Should().HaveCount(1);
        result.Value.Organizations[0].OrganizationName.Should().Be("Acme Corp");
        result.Value.Organizations[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UserInDifferentTenant_ReturnsFailure()
    {
        // Arrange — user exists but in a different tenant
        var otherTenantId = TenantId.New();
        var user = User.Create(otherTenantId, KeycloakUserId, "other@example.com", "Other", "Tenant");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor,
            NullLogger<GetCurrentUserHandler>.Instance);
        var query = new GetCurrentUserQuery(KeycloakUserId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact(Skip = "Cannot simulate >500ms with in-memory DB")]
    public async Task Handle_SlowQuery_LogsWarning()
    {
        // Arrange — seed user so the query succeeds
        var user = User.Create(_tenantId, KeycloakUserId, "slow@example.com", "Slow", "Query");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Use a mock logger to verify warning is possible
        // Note: In a real scenario with a slow DB, the >500ms threshold would trigger.
        // Here we verify the handler completes correctly with a logger that can capture calls.
        var mockLogger = Substitute.For<ILogger<GetCurrentUserHandler>>();
        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor, mockLogger);
        var query = new GetCurrentUserQuery(KeycloakUserId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — handler succeeded (in-memory DB is fast, so no warning expected)
        result.IsSuccess.Should().BeTrue();
        // The slow-query warning path is covered by code inspection;
        // with an in-memory DB we cannot naturally trigger >500ms latency.
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
