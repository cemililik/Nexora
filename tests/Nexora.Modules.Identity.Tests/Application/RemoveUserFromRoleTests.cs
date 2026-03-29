using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class RemoveUserFromRoleTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Organization _org;
    private readonly User _user;
    private readonly Role _role;

    public RemoveUserFromRoleTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);

        _org = Organization.Create(_tenantId, "Test Org", "test-org");
        _user = User.Create(_tenantId, "kc-1", "john@test.com", "John", "Doe");
        _role = Role.Create(_tenantId, "Admin");
        _dbContext.Organizations.Add(_org);
        _dbContext.Users.Add(_user);
        _dbContext.Roles.Add(_role);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Handle_ValidCommand_RemovesUserFromRole()
    {
        // Arrange: user has org membership and role assignment
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var userRole = UserRole.Create(orgUser.Id, _role.Id);
        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync();

        var handler = new RemoveUserFromRoleHandler(_dbContext, _tenantAccessor, NullLogger<RemoveUserFromRoleHandler>.Instance);
        var command = new RemoveUserFromRoleCommand(_role.Id.Value, _user.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var count = await _dbContext.UserRoles.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RoleNotFound_ReturnsFailure()
    {
        // User has no org memberships, so handler returns user_not_found
        var handler = new RemoveUserFromRoleHandler(_dbContext, _tenantAccessor, NullLogger<RemoveUserFromRoleHandler>.Instance);
        var command = new RemoveUserFromRoleCommand(_role.Id.Value, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact]
    public async Task Handle_UserNotInRole_ReturnsFailure()
    {
        // Arrange: user has org membership but no role assignment
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new RemoveUserFromRoleHandler(_dbContext, _tenantAccessor, NullLogger<RemoveUserFromRoleHandler>.Instance);
        var command = new RemoveUserFromRoleCommand(_role.Id.Value, _user.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_in_role");
    }

    [Fact]
    public void Validator_EmptyRoleId_FailsValidation()
    {
        var validator = new RemoveUserFromRoleValidator();
        var command = new RemoveUserFromRoleCommand(Guid.Empty, Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RoleId)
            .WithErrorMessage("lockey_validation_required");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
