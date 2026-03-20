using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    [Fact]
    public void Create_Individual_ShouldSetProperties()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, "john@example.com", "+1234567890",
            ContactSource.Manual);

        // Act & Assert
        contact.Id.Value.Should().NotBeEmpty();
        contact.TenantId.Should().Be(_tenantId);
        contact.OrganizationId.Should().Be(_orgId);
        contact.Type.Should().Be(ContactType.Individual);
        contact.FirstName.Should().Be("John");
        contact.LastName.Should().Be("Doe");
        contact.DisplayName.Should().Be("John Doe");
        contact.Email.Should().Be("john@example.com");
        contact.Phone.Should().Be("+1234567890");
        contact.Source.Should().Be(ContactSource.Manual);
        contact.Status.Should().Be(ContactStatus.Active);
        contact.MergedIntoId.Should().BeNull();
    }

    [Fact]
    public void Create_Organization_ShouldSetDisplayName()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Organization,
            null, null, "Acme Corp", "info@acme.com", null,
            ContactSource.Api);

        // Act & Assert
        contact.DisplayName.Should().Be("Acme Corp");
        contact.CompanyName.Should().Be("Acme Corp");
        contact.Type.Should().Be(ContactType.Organization);
    }

    [Fact]
    public void Create_ShouldNormalizeEmail()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "Jane", "Smith", null, "  JANE@Example.COM  ", null,
            ContactSource.WebForm);

        // Act & Assert
        contact.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, "john@example.com", null,
            ContactSource.Manual);

        // Act & Assert
        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ContactCreatedEvent>();
    }

    [Fact]
    public void Create_ShouldSetDefaultLanguageAndCurrency()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, null, null,
            ContactSource.Manual);

        // Act & Assert
        contact.Language.Should().Be("en");
        contact.Currency.Should().Be("USD");
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "Old", "Name", null, "old@test.com", null,
            ContactSource.Manual);
        contact.ClearDomainEvents();

        // Act
        contact.Update("New", "Name", null, "new@test.com", "+905551234567",
            "+905559876543", "https://example.com", "12345", "tr", "TRY", "Dr");

        // Assert
        contact.FirstName.Should().Be("New");
        contact.DisplayName.Should().Be("New Name");
        contact.Email.Should().Be("new@test.com");
        contact.Phone.Should().Be("+905551234567");
        contact.Mobile.Should().Be("+905559876543");
        contact.Website.Should().Be("https://example.com");
        contact.TaxId.Should().Be("12345");
        contact.Language.Should().Be("tr");
        contact.Currency.Should().Be("TRY");
        contact.Title.Should().Be("Dr");
    }

    [Fact]
    public void Update_ShouldRaiseDomainEvent()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, null, null,
            ContactSource.Manual);
        contact.ClearDomainEvents();

        // Act
        contact.Update("Jane", "Doe", null, null, null, null, null, null, "en", "USD");

        // Assert
        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ContactUpdatedEvent>();
    }

    [Fact]
    public void Archive_ShouldChangeStatus()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, null, null,
            ContactSource.Manual);
        contact.ClearDomainEvents();

        // Act
        contact.Archive();

        // Assert
        contact.Status.Should().Be(ContactStatus.Archived);
    }

    [Fact]
    public void Archive_ShouldRaiseDomainEvent()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, null, null,
            ContactSource.Manual);
        contact.ClearDomainEvents();

        // Act
        contact.Archive();

        // Assert
        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ContactArchivedEvent>();
    }

    [Fact]
    public void Restore_ShouldChangeStatus()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, null, null,
            ContactSource.Manual);
        contact.Archive();
        contact.ClearDomainEvents();

        // Act
        contact.Restore();

        // Assert
        contact.Status.Should().Be(ContactStatus.Active);
    }

    [Fact]
    public void Restore_ShouldRaiseDomainEvent()
    {
        // Arrange
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, null, null,
            ContactSource.Manual);
        contact.Archive();
        contact.ClearDomainEvents();

        // Act
        contact.Restore();

        // Assert
        contact.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ContactRestoredEvent>();
    }

    [Fact]
    public void MarkMerged_ShouldSetStatusAndTarget()
    {
        // Arrange
        var primary = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "Primary", "Contact", null, null, null,
            ContactSource.Manual);
        var secondary = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "Secondary", "Contact", null, null, null,
            ContactSource.Manual);
        secondary.ClearDomainEvents();

        // Act
        secondary.MarkMerged(primary.Id);

        // Assert
        secondary.Status.Should().Be(ContactStatus.Merged);
        secondary.MergedIntoId.Should().Be(primary.Id);
    }

    [Fact]
    public void MarkMerged_ShouldRaiseDomainEvent()
    {
        // Arrange
        var primary = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "Primary", "Contact", null, null, null,
            ContactSource.Manual);
        var secondary = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            "Secondary", "Contact", null, null, null,
            ContactSource.Manual);
        secondary.ClearDomainEvents();

        // Act
        secondary.MarkMerged(primary.Id);

        // Assert
        secondary.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ContactMergedEvent>();
    }
}
