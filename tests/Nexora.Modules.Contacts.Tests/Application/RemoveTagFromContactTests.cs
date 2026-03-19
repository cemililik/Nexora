using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class RemoveTagFromContactTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public RemoveTagFromContactTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingAssignment_ShouldRemove()
    {
        // Arrange
        var (contact, tag) = await SeedContactWithTag();

        var handler = new RemoveTagFromContactHandler(_dbContext, _tenantAccessor, NullLogger<RemoveTagFromContactHandler>.Instance);
        var result = await handler.Handle(
            new RemoveTagFromContactCommand(contact.Id.Value, tag.Id.Value),
            CancellationToken.None);

        // Act
        result.IsSuccess.Should().BeTrue();

        // Assert
        var count = await _dbContext.ContactTags.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new RemoveTagFromContactHandler(_dbContext, _tenantAccessor, NullLogger<RemoveTagFromContactHandler>.Instance);
        var result = await handler.Handle(
            new RemoveTagFromContactCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_TagNotAssigned_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new RemoveTagFromContactHandler(_dbContext, _tenantAccessor, NullLogger<RemoveTagFromContactHandler>.Instance);
        var result = await handler.Handle(
            new RemoveTagFromContactCommand(contact.Id.Value, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_assigned");
    }

    [Fact]
    public async Task Handle_DifferentOrgAssignment_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var tag = Tag.Create(_tenantId, "Tag", TagCategory.Donor);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Assign in a different org
        var otherOrgTag = ContactTag.Create(contact.Id, tag.Id, Guid.NewGuid());
        await _dbContext.ContactTags.AddAsync(otherOrgTag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new RemoveTagFromContactHandler(_dbContext, _tenantAccessor, NullLogger<RemoveTagFromContactHandler>.Instance);
        var result = await handler.Handle(
            new RemoveTagFromContactCommand(contact.Id.Value, tag.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_assigned");
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessMessage()
    {
        // Arrange
        var (contact, tag) = await SeedContactWithTag();

        // Act
        var handler = new RemoveTagFromContactHandler(_dbContext, _tenantAccessor, NullLogger<RemoveTagFromContactHandler>.Instance);
        var result = await handler.Handle(
            new RemoveTagFromContactCommand(contact.Id.Value, tag.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message!.Key.Should().Be("lockey_contacts_tag_removed");
    }

    private async Task<(Contact contact, Tag tag)> SeedContactWithTag()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var tag = Tag.Create(_tenantId, "VIP", TagCategory.Donor);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        var contactTag = ContactTag.Create(contact.Id, tag.Id, _orgId);
        await _dbContext.ContactTags.AddAsync(contactTag);
        await _dbContext.SaveChangesAsync();

        return (contact, tag);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
