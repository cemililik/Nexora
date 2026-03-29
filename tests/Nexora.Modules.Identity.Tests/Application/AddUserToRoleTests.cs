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

public sealed class AddUserToRoleTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Organization _org;
    private readonly User _user;
    private readonly Role _role;

    public AddUserToRoleTests()
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
    public async Task Handle_ValidCommand_AddsUserToRole()
    {
        // Arrange: user must have an org membership
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new AddUserToRoleHandler(_dbContext, _tenantAccessor, NullLogger<AddUserToRoleHandler>.Instance);
        var command = new AddUserToRoleCommand(_role.Id.Value, _user.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var userRoleCount = await _dbContext.UserRoles.CountAsync();
        userRoleCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RoleNotFound_ReturnsFailure()
    {
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new AddUserToRoleHandler(_dbContext, _tenantAccessor, NullLogger<AddUserToRoleHandler>.Instance);
        var command = new AddUserToRoleCommand(Guid.NewGuid(), _user.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_not_found");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // User with no org memberships
        var handler = new AddUserToRoleHandler(_dbContext, _tenantAccessor, NullLogger<AddUserToRoleHandler>.Instance);
        var command = new AddUserToRoleCommand(_role.Id.Value, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_in_org");
    }

    [Fact]
    public async Task Handle_UserAlreadyHasRole_ReturnsFailure()
    {
        // Arrange: user has org membership and already has the role
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var userRole = UserRole.Create(orgUser.Id, _role.Id);
        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync();

        var handler = new AddUserToRoleHandler(_dbContext, _tenantAccessor, NullLogger<AddUserToRoleHandler>.Instance);
        var command = new AddUserToRoleCommand(_role.Id.Value, _user.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_already_has_role");
    }

    [Fact]
    public void Validator_EmptyRoleId_FailsValidation()
    {
        var validator = new AddUserToRoleValidator();
        var command = new AddUserToRoleCommand(Guid.Empty, Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RoleId)
            .WithErrorMessage("lockey_validation_required");
    }

    [Fact]
    public void Validator_EmptyUserId_FailsValidation()
    {
        var validator = new AddUserToRoleValidator();
        var command = new AddUserToRoleCommand(Guid.NewGuid(), Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
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
