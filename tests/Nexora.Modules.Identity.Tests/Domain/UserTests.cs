using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class UserTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var user = User.Create(_tenantId, "kc-123", "john@example.com", "John", "Doe");

        user.Id.Value.Should().NotBeEmpty();
        user.TenantId.Should().Be(_tenantId);
        user.KeycloakUserId.Should().Be("kc-123");
        user.Email.Should().Be("john@example.com");
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Status.Should().Be(UserStatus.Active);
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.UserCreatedEvent>();
    }

    [Fact]
    public void Create_ShouldNormalizeEmail()
    {
        var user = User.Create(_tenantId, "kc-1", "JOHN@EXAMPLE.COM", "John", "Doe");

        user.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void FullName_ShouldCombineFirstAndLast()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");

        user.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void UpdateProfile_ShouldChangeFields()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");

        user.UpdateProfile("Jane", "Smith", "+905551234567");

        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.Phone.Should().Be("+905551234567");
    }

    [Fact]
    public void RecordLogin_ShouldSetTimestamp()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");

        user.RecordLogin();

        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");

        user.Deactivate();

        user.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public void Deactivate_ShouldRaiseDomainEvent()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");
        user.ClearDomainEvents();

        user.Deactivate();

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.UserDeactivatedEvent>();
    }

    [Fact]
    public void Activate_ShouldSetActive()
    {
        var user = User.Create(_tenantId, "kc-1", "j@test.com", "John", "Doe");
        user.Deactivate();

        user.Activate();

        user.Status.Should().Be(UserStatus.Active);
    }
}
