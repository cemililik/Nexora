using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.Services;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class DuplicateDetectionServiceTests
{
    private readonly DuplicateDetectionService _sut = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    [Fact]
    public void CalculateScore_ExactEmailMatch_ShouldReturn40()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, "john@test.com", null);
        var candidate = CreateContact("Jane", "Smith", null, "john@test.com", null);

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(40);
    }

    [Fact]
    public void CalculateScore_ExactPhoneMatch_ShouldReturn30()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, null, "+1-555-1234");
        var candidate = CreateContact("Jane", "Smith", null, null, "15551234");

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(30);
    }

    [Fact]
    public void CalculateScore_ExactNameMatch_ShouldReturn25()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, null, null);
        var candidate = CreateContact("John", "Doe", null, null, null);

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(25); // 10 first + 15 last
    }

    [Fact]
    public void CalculateScore_EmailAndNameMatch_ShouldReturn65()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, "john@test.com", null);
        var candidate = CreateContact("John", "Doe", null, "john@test.com", null);

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(65); // 40 email + 10 first + 15 last
    }

    [Fact]
    public void CalculateScore_FuzzyFirstName_ShouldReturnPartialScore()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, null, null);
        var candidate = CreateContact("Jonh", "Doe", null, null, null); // typo

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(20); // 5 fuzzy first + 15 exact last
    }

    [Fact]
    public void CalculateScore_FuzzyLastName_ShouldReturnPartialScore()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, null, null);
        var candidate = CreateContact("John", "Does", null, null, null); // close

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(18); // 10 exact first + 8 fuzzy last
    }

    [Fact]
    public void CalculateScore_NoMatch_ShouldReturnZero()
    {
        // Arrange
        var source = CreateContact("John", "Doe", null, "john@test.com", "1234");
        var candidate = CreateContact("Alice", "Wonder", null, "alice@other.com", "9999");

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateScore_CompanyMatch_ShouldAdd5()
    {
        // Arrange
        var source = CreateContact("John", "Doe", "Acme Corp", null, null);
        var candidate = CreateContact("Jane", "Smith", "Acme Corp", null, null);

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(5);
    }

    [Fact]
    public void CalculateScore_FullMatch_ShouldCapAt100()
    {
        // Arrange
        var source = CreateContact("John", "Doe", "Acme Corp", "john@test.com", "+1-555-1234");
        var candidate = CreateContact("John", "Doe", "Acme Corp", "john@test.com", "15551234");

        // Act
        var score = _sut.CalculateScore(source, candidate);

        // Assert
        score.Should().Be(100); // 40+30+25+5=100
    }

    private Contact CreateContact(string? firstName, string? lastName, string? companyName, string? email, string? phone)
    {
        return Contact.Create(_tenantId, _orgId, ContactType.Individual, firstName, lastName, companyName, email, phone, ContactSource.Manual);
    }
}
