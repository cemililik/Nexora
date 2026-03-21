using Nexora.Modules.Identity.Domain.Entities;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class TenantTests
{
    [Fact]
    public void Create_ValidTenant_SetsProperties()
    {
        var tenant = Tenant.Create("Acme Corp", "acme-corp");

        tenant.Id.Value.Should().NotBeEmpty();
        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme-corp");
        tenant.Status.Should().Be(TenantStatus.Trial);
        tenant.RealmId.Should().BeNull();
    }

    [Fact]
    public void Create_ValidTenant_RaisesDomainEvent()
    {
        var tenant = Tenant.Create("Acme Corp", "acme-corp");

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantCreatedEvent>();
    }

    [Fact]
    public void Create_UppercaseSlug_NormalizesSlug()
    {
        var tenant = Tenant.Create("Test", "UPPER-SLUG");

        tenant.Slug.Should().Be("upper-slug");
    }

    [Fact]
    public void Activate_TrialTenant_ChangesStatus()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void Activate_TrialTenant_RaisesDomainEvent()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantStatusChangedEvent>();
    }

    [Fact]
    public void Suspend_ActiveTenant_ChangesStatus()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();

        tenant.Suspend();

        tenant.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public void Terminate_TrialTenant_ChangesStatus()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.Terminate();

        tenant.Status.Should().Be(TenantStatus.Terminated);
    }

    [Fact]
    public void Activate_FromTrial_RaisesDomainEvent()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantStatusChangedEvent>();
    }

    [Fact]
    public void Suspend_ActiveTenant_ChangesStatusAndRaisesDomainEvent()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.ClearDomainEvents();

        tenant.Suspend();

        tenant.Status.Should().Be(TenantStatus.Suspended);
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantStatusChangedEvent>();
    }

    [Fact]
    public void Suspend_SuspendedTenant_IsNoOp()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.Suspend();
        tenant.ClearDomainEvents();

        tenant.Suspend();

        tenant.Status.Should().Be(TenantStatus.Suspended);
        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Terminate_SuspendedTenant_ChangesStatusAndRaisesDomainEvent()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.Suspend();
        tenant.ClearDomainEvents();

        tenant.Terminate();

        tenant.Status.Should().Be(TenantStatus.Terminated);
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.TenantStatusChangedEvent>();
    }

    [Fact]
    public void Terminate_TerminatedTenant_IsNoOp()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Terminate();
        tenant.ClearDomainEvents();

        tenant.Terminate();

        tenant.Status.Should().Be(TenantStatus.Terminated);
        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_ActiveTenant_IsNoOp()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Suspend_TrialTenant_ThrowsDomainException()
    {
        var tenant = Tenant.Create("Test", "test");

        var act = () => tenant.Suspend();

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_error_tenant_suspension_not_allowed");
    }

    [Fact]
    public void Suspend_TerminatedTenant_ThrowsDomainException()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Terminate();

        var act = () => tenant.Suspend();

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_error_tenant_suspension_not_allowed");
    }

    [Fact]
    public void Activate_TerminatedTenant_ThrowsDomainException()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Terminate();

        var act = () => tenant.Activate();

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_error_tenant_activation_not_allowed");
    }

    [Fact]
    public void Activate_SuspendedTenant_ChangesStatus()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Activate();
        tenant.Suspend();

        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void SetRealmId_ValidRealmId_SetsValue()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.SetRealmId("realm-123");

        tenant.RealmId.Should().Be("realm-123");
    }

    [Fact]
    public void SetRealmId_RealmIdWithWhitespace_TrimsValue()
    {
        var tenant = Tenant.Create("Test", "test");

        tenant.SetRealmId("  realm-123  ");

        tenant.RealmId.Should().Be("realm-123");
    }

    [Fact]
    public void SetRealmId_NullOrEmptyValue_ThrowsDomainException()
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
