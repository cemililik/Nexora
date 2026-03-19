using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactMergeServiceTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactMergeServiceTests()
    {
        var tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, tenantAccessor);
    }

    [Fact]
    public async Task MergeAsync_ShouldMarkSecondaryAsMerged()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var service = new ContactMergeService(_dbContext);

        // Act
        await service.MergeAsync(primary, secondary, null, CancellationToken.None);

        // Assert
        secondary.Status.Should().Be(ContactStatus.Merged);
        secondary.MergedIntoId.Should().Be(primary.Id);
    }

    [Fact]
    public async Task MergeAsync_ShouldTransferTags()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var tag1 = Tag.Create(_tenantId, "Tag1", TagCategory.Donor);
        var tag2 = Tag.Create(_tenantId, "Tag2", TagCategory.Volunteer);
        await _dbContext.Tags.AddRangeAsync(tag1, tag2);
        await _dbContext.SaveChangesAsync();

        var primaryTag = ContactTag.Create(primary.Id, tag1.Id, _orgId);
        var secondaryTag = ContactTag.Create(secondary.Id, tag2.Id, _orgId);
        await _dbContext.ContactTags.AddRangeAsync(primaryTag, secondaryTag);
        await _dbContext.SaveChangesAsync();

        // Act
        var service = new ContactMergeService(_dbContext);
        await service.MergeAsync(primary, secondary, null, CancellationToken.None);

        // Assert
        var primaryTags = await _dbContext.ContactTags.Where(t => t.ContactId == primary.Id).ToListAsync();
        primaryTags.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeAsync_ShouldNotDuplicateTags()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var tag = Tag.Create(_tenantId, "SharedTag", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        var pt = ContactTag.Create(primary.Id, tag.Id, _orgId);
        var st = ContactTag.Create(secondary.Id, tag.Id, _orgId);
        await _dbContext.ContactTags.AddRangeAsync(pt, st);
        await _dbContext.SaveChangesAsync();

        // Act
        var service = new ContactMergeService(_dbContext);
        await service.MergeAsync(primary, secondary, null, CancellationToken.None);

        // Assert
        var primaryTags = await _dbContext.ContactTags.Where(t => t.ContactId == primary.Id).ToListAsync();
        primaryTags.Should().HaveCount(1);
    }

    [Fact]
    public async Task MergeAsync_ShouldTransferRelationships()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var thirdContact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Alice", "Wonder", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(thirdContact);
        await _dbContext.SaveChangesAsync();

        var rel = ContactRelationship.Create(secondary.Id, thirdContact.Id, RelationshipType.ParentOf);
        await _dbContext.ContactRelationships.AddAsync(rel);
        await _dbContext.SaveChangesAsync();

        // Act
        var service = new ContactMergeService(_dbContext);
        await service.MergeAsync(primary, secondary, null, CancellationToken.None);

        // Assert
        var primaryRels = await _dbContext.ContactRelationships.Where(r => r.ContactId == primary.Id).ToListAsync();
        primaryRels.Should().HaveCount(1);
        primaryRels[0].RelatedContactId.Should().Be(thirdContact.Id);
    }

    [Fact]
    public async Task MergeAsync_WithFieldSelections_ShouldApplySecondaryEmail()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts("john@primary.com", "jane@secondary.com");
        var service = new ContactMergeService(_dbContext);

        // Act
        await service.MergeAsync(primary, secondary, new MergeFieldSelections(UseSecondaryEmail: true), CancellationToken.None);

        // Assert
        primary.Email.Should().Be("jane@secondary.com");
    }

    [Fact]
    public async Task MergeAsync_ShouldTransferCustomFields()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var definition = CustomFieldDefinition.Create(_tenantId, "Nickname", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var field = ContactCustomField.Create(secondary.Id, definition.Id, "Johnny");
        await _dbContext.ContactCustomFields.AddAsync(field);
        await _dbContext.SaveChangesAsync();

        // Act
        var service = new ContactMergeService(_dbContext);
        await service.MergeAsync(primary, secondary, null, CancellationToken.None);

        // Assert
        var primaryFields = await _dbContext.ContactCustomFields.Where(f => f.ContactId == primary.Id).ToListAsync();
        primaryFields.Should().HaveCount(1);
        primaryFields[0].Value.Should().Be("Johnny");
    }

    private async Task<(Contact primary, Contact secondary)> SeedTwoContacts(
        string? primaryEmail = null, string? secondaryEmail = null)
    {
        var primary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, primaryEmail, null, ContactSource.Manual);
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, secondaryEmail, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(primary, secondary);
        await _dbContext.SaveChangesAsync();
        return (primary, secondary);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
