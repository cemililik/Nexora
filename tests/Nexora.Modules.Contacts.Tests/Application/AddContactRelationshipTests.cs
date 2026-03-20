using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddContactRelationshipTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public AddContactRelationshipTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidRelationship_ShouldCreate()
    {
        // Arrange
        var (contact, related) = await SeedTwoContacts();
        var handler = new AddContactRelationshipHandler(_dbContext, _tenantAccessor, NullLogger<AddContactRelationshipHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new AddContactRelationshipCommand(contact.Id.Value, related.Id.Value, "ParentOf"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be("ParentOf");
        result.Value.RelatedContactDisplayName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new AddContactRelationshipHandler(_dbContext, _tenantAccessor, NullLogger<AddContactRelationshipHandler>.Instance);
        var result = await handler.Handle(
            new AddContactRelationshipCommand(Guid.NewGuid(), Guid.NewGuid(), "ParentOf"),
            CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_RelatedContactNotFound_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddContactRelationshipHandler(_dbContext, _tenantAccessor, NullLogger<AddContactRelationshipHandler>.Instance);
        var result = await handler.Handle(
            new AddContactRelationshipCommand(contact.Id.Value, Guid.NewGuid(), "ParentOf"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_related_contact_not_found");
    }

    [Fact]
    public async Task Handle_DuplicateRelationship_ShouldFail()
    {
        // Arrange
        var (contact, related) = await SeedTwoContacts();
        var existing = ContactRelationship.Create(contact.Id, related.Id, RelationshipType.ParentOf);
        await _dbContext.ContactRelationships.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddContactRelationshipHandler(_dbContext, _tenantAccessor, NullLogger<AddContactRelationshipHandler>.Instance);
        var result = await handler.Handle(
            new AddContactRelationshipCommand(contact.Id.Value, related.Id.Value, "ParentOf"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_relationship_already_exists");
    }

    [Fact]
    public async Task Handle_SameContactsDifferentType_ShouldSucceed()
    {
        // Arrange
        var (contact, related) = await SeedTwoContacts();
        var existing = ContactRelationship.Create(contact.Id, related.Id, RelationshipType.ParentOf);
        await _dbContext.ContactRelationships.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddContactRelationshipHandler(_dbContext, _tenantAccessor, NullLogger<AddContactRelationshipHandler>.Instance);
        var result = await handler.Handle(
            new AddContactRelationshipCommand(contact.Id.Value, related.Id.Value, "EmployerOf"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    private async Task<(Contact contact, Contact related)> SeedTwoContacts()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var related = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(contact, related);
        await _dbContext.SaveChangesAsync();
        return (contact, related);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
