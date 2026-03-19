using Nexora.Modules.Identity.Domain.Entities;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class TenantTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var tenant = Tenant.Create("Acme Corp", "acme-corp");

        tenant.Id.Value.Should().NotBeEmpty();
        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme-corp");
        tenant.Status.Should().Be(TenantStatus.Trial);
        tenant.RealmId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        var tenant = Tenant.Create("Acme Corp", "acme-corp");

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantCreatedEvent>();
    }

    [Fact]
    public void Create_ShouldNormalizeSlug()
    {
        var tenant = Tenant.Create("Test", "UPPER-SLUG");

        tenant.Slug.Should().Be("upper-slug");
    }

    [Fact]
    public void Activate_ShouldChangeStatus()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void Activate_ShouldRaiseDomainEvent()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantStatusChangedEvent>();
    }

    [Fact]
    public void Suspend_ShouldChangeStatus()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();

        tenant.Suspend();

        tenant.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public void Terminate_ShouldChangeStatus()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.Terminate();

        tenant.Status.Should().Be(TenantStatus.Terminated);
    }

    [Fact]
    public void SetRealmId_ShouldSetValue()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.SetRealmId("realm-123");

        tenant.RealmId.Should().Be("realm-123");
    }
}
