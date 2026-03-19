using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddTagToContactTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public AddTagToContactTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidAssignment_ShouldSucceed()
    {
        // Arrange
        var (contact, tag) = await SeedContactAndTag();

        // Act
        var handler = new AddTagToContactHandler(_dbContext, _tenantAccessor, NullLogger<AddTagToContactHandler>.Instance);
        var result = await handler.Handle(
            new AddTagToContactCommand(contact.Id.Value, tag.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContactId.Should().Be(contact.Id.Value);
        result.Value.TagId.Should().Be(tag.Id.Value);
        result.Value.TagName.Should().Be(tag.Name);
        result.Value.TagCategory.Should().Be(tag.Category.ToString());
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var (contact, tag) = await SeedContactAndTag();

        // Act
        var handler = new AddTagToContactHandler(_dbContext, _tenantAccessor, NullLogger<AddTagToContactHandler>.Instance);
        await handler.Handle(new AddTagToContactCommand(contact.Id.Value, tag.Id.Value), CancellationToken.None);

        // Assert
        var count = await _dbContext.ContactTags.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Tag", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddTagToContactHandler(_dbContext, _tenantAccessor, NullLogger<AddTagToContactHandler>.Instance);
        var result = await handler.Handle(
            new AddTagToContactCommand(Guid.NewGuid(), tag.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_TagNotFound_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddTagToContactHandler(_dbContext, _tenantAccessor, NullLogger<AddTagToContactHandler>.Instance);
        var result = await handler.Handle(
            new AddTagToContactCommand(contact.Id.Value, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_found");
    }

    [Fact]
    public async Task Handle_InactiveTag_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var tag = Tag.Create(_tenantId, "Inactive", TagCategory.Donor);
        tag.Deactivate();
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddTagToContactHandler(_dbContext, _tenantAccessor, NullLogger<AddTagToContactHandler>.Instance);
        var result = await handler.Handle(
            new AddTagToContactCommand(contact.Id.Value, tag.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_found");
    }

    [Fact]
    public async Task Handle_AlreadyAssigned_ShouldFail()
    {
        // Arrange
        var (contact, tag) = await SeedContactAndTag();
        var existing = ContactTag.Create(contact.Id, tag.Id, _orgId);
        await _dbContext.ContactTags.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddTagToContactHandler(_dbContext, _tenantAccessor, NullLogger<AddTagToContactHandler>.Instance);
        var result = await handler.Handle(
            new AddTagToContactCommand(contact.Id.Value, tag.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_already_assigned");
    }

    private async Task<(Contact contact, Tag tag)> SeedContactAndTag()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var tag = Tag.Create(_tenantId, "VIP", TagCategory.Donor, "#FF0000");
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.Tags.AddAsync(tag);
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
