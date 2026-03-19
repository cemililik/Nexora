using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class OrganizationTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var org = Organization.Create(_tenantId, "Acme School", "acme-school");

        org.Id.Value.Should().NotBeEmpty();
        org.TenantId.Should().Be(_tenantId);
        org.Name.Should().Be("Acme School");
        org.Slug.Should().Be("acme-school");
        org.Timezone.Should().Be("UTC");
        org.DefaultCurrency.Should().Be("USD");
        org.DefaultLanguage.Should().Be("en");
        org.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        var org = Organization.Create(_tenantId, "Test", "test");

        org.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.OrganizationCreatedEvent>();
    }

    [Fact]
    public void Create_ShouldNormalizeSlug()
    {
        var org = Organization.Create(_tenantId, "Test", "UPPER");

        org.Slug.Should().Be("upper");
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        var org = Organization.Create(_tenantId, "Old Name", "old");

        org.Update("New Name", "Europe/Istanbul", "TRY", "tr");

        org.Name.Should().Be("New Name");
        org.Timezone.Should().Be("Europe/Istanbul");
        org.DefaultCurrency.Should().Be("TRY");
        org.DefaultLanguage.Should().Be("tr");
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var org = Organization.Create(_tenantId, "Test", "test");

        org.Deactivate();

        org.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetActive()
    {
        var org = Organization.Create(_tenantId, "Test", "test");
        org.Deactivate();

        org.Activate();

        org.IsActive.Should().BeTrue();
    }
}
