using Nexora.Modules.Identity.Domain.Entities;
using Nexora.SharedKernel.Domain.Exceptions;

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
    public void Activate_WhenAlreadyActive_ShouldBeNoOp()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Suspend_FromTrial_ShouldThrow()
    {
        var tenant = Tenant.Create("Test", "test");

        var act = () => tenant.Suspend();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Suspend_FromTerminated_ShouldThrow()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Terminate();

        var act = () => tenant.Suspend();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Activate_FromTerminated_ShouldThrow()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Terminate();

        var act = () => tenant.Activate();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Activate_FromSuspended_ShouldChangeStatus()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.Suspend();

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void SetRealmId_ShouldSetValue()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.SetRealmId("realm-123");

        tenant.RealmId.Should().Be("realm-123");
    }

    [Fact]
    public void SetRealmId_ShouldTrimValue()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.SetRealmId("  realm-123  ");

        tenant.RealmId.Should().Be("realm-123");
    }

    [Fact]
    public void SetRealmId_NullOrEmpty_ShouldThrow()
    {
        var tenant = Tenant.Create("Test", "test");

        var act1 = () => tenant.SetRealmId(null!);
        var act2 = () => tenant.SetRealmId("");
        var act3 = () => tenant.SetRealmId("   ");

        act1.Should().Throw<DomainException>();
        act2.Should().Throw<DomainException>();
        act3.Should().Throw<DomainException>();
    }
}
