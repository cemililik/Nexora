using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactNameTests
{
    [Fact]
    public void ForIndividual_ShouldComputeDisplayName()
    {
        // Arrange
        var name = ContactName.ForIndividual("John", "Doe");

        // Act & Assert
        name.DisplayName.Should().Be("John Doe");
        name.FirstName.Should().Be("John");
        name.LastName.Should().Be("Doe");
        name.CompanyName.Should().BeNull();
    }

    [Fact]
    public void ForOrganization_ShouldUseCompanyName()
    {
        // Arrange
        var name = ContactName.ForOrganization("Acme Corp");

        // Act & Assert
        name.DisplayName.Should().Be("Acme Corp");
        name.FirstName.Should().BeNull();
        name.LastName.Should().BeNull();
        name.CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public void Create_OnlyFirstName_ShouldUseFirstName()
    {
        // Arrange
        var name = ContactName.Create("Jane", null, null);

        // Act & Assert
        name.DisplayName.Should().Be("Jane");
    }

    [Fact]
    public void Create_OnlyLastName_ShouldUseLastName()
    {
        // Arrange
        var name = ContactName.Create(null, "Smith", null);

        // Act & Assert
        name.DisplayName.Should().Be("Smith");
    }

    [Fact]
    public void Create_AllEmpty_ShouldThrowDomainException()
    {
        // Arrange
        var act = () => ContactName.Create(null, null, null);

        // Act & Assert
        act.Should().Throw<DomainException>()
            .WithMessage("lockey_contacts_error_name_required");
    }

    [Fact]
    public void Create_ShouldTrimWhitespace()
    {
        // Arrange
        var name = ContactName.Create("  John  ", "  Doe  ", null);

        // Act & Assert
        name.FirstName.Should().Be("John");
        name.LastName.Should().Be("Doe");
        name.DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public void Create_FirstLastAndCompany_ShouldPreferFirstLast()
    {
        // Arrange
        var name = ContactName.Create("Jane", "Smith", "Acme Corp");

        // Act & Assert
        name.DisplayName.Should().Be("Jane Smith");
        name.CompanyName.Should().Be("Acme Corp");
    }
}
