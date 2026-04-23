using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class UpdateUserPreferencesTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private const string KeycloakUserId = "kc-pref-user";

    public UpdateUserPreferencesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidLanguage_SetsPreferredLanguage()
    {
        var user = User.Create(_tenantId, KeycloakUserId, "pref@example.com", "Pref", "User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserPreferencesHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateUserPreferencesHandler>.Instance);
        var command = new UpdateUserPreferencesCommand(KeycloakUserId, "tr");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _dbContext.Users.FirstAsync();
        updated.PreferredLanguage.Should().Be("tr");
    }

    [Fact]
    public async Task Handle_NullLanguage_ClearsPreferredLanguage()
    {
        var user = User.Create(_tenantId, KeycloakUserId, "clear@example.com", "Clear", "Pref");
        user.UpdatePreferences("tr");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserPreferencesHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateUserPreferencesHandler>.Instance);
        var command = new UpdateUserPreferencesCommand(KeycloakUserId, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Users.FirstAsync();
        updated.PreferredLanguage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var handler = new UpdateUserPreferencesHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateUserPreferencesHandler>.Instance);
        var command = new UpdateUserPreferencesCommand("nonexistent-kc", "en");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact]
    public async Task Handle_WhitespaceLanguage_ClearsPreferredLanguage()
    {
        var user = User.Create(_tenantId, KeycloakUserId, "ws@example.com", "White", "Space");
        user.UpdatePreferences("tr");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserPreferencesHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateUserPreferencesHandler>.Instance);
        var command = new UpdateUserPreferencesCommand(KeycloakUserId, "   ");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Users.FirstAsync();
        updated.PreferredLanguage.Should().BeNull();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
