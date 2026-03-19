using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetContact360Tests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly ContactActivityContributorAggregator _aggregator;

    public GetContact360Tests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
        _aggregator = new ContactActivityContributorAggregator([]);
    }

    [Fact]
    public async Task Handle_ExistingContact_ShouldReturnFull360View()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Add some sub-entities
        var note = ContactNote.Create(contact.Id, Guid.NewGuid(), _orgId, "Important note");
        note.Pin();
        await _dbContext.ContactNotes.AddAsync(note);

        var consent = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web");
        await _dbContext.ConsentRecords.AddAsync(consent);

        var activity = ContactActivity.Create(contact.Id, _orgId, "contacts", "Created", "Contact created");
        await _dbContext.ContactActivities.AddAsync(activity);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContact360Handler(_dbContext, _tenantAccessor, _aggregator, NullLogger<GetContact360Handler>.Instance);
        var result = await handler.Handle(new GetContact360Query(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Contact.DisplayName.Should().Be("John Doe");
        result.Value.RecentNotes.Should().HaveCount(1);
        result.Value.RecentNotes[0].IsPinned.Should().BeTrue();
        result.Value.ConsentRecords.Should().HaveCount(1);
        result.Value.RecentActivities.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new GetContact360Handler(_dbContext, _tenantAccessor, _aggregator, NullLogger<GetContact360Handler>.Instance);
        var result = await handler.Handle(new GetContact360Query(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithRelationships_ShouldIncludeDisplayNames()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var related = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(contact, related);
        await _dbContext.SaveChangesAsync();

        var rel = ContactRelationship.Create(contact.Id, related.Id, RelationshipType.ParentOf);
        await _dbContext.ContactRelationships.AddAsync(rel);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContact360Handler(_dbContext, _tenantAccessor, _aggregator, NullLogger<GetContact360Handler>.Instance);
        var result = await handler.Handle(new GetContact360Query(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Relationships.Should().HaveCount(1);
        result.Value.Relationships[0].RelatedContactDisplayName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task Handle_WithCustomFields_ShouldInclude()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        var definition = CustomFieldDefinition.Create(_tenantId, "Nickname", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var field = ContactCustomField.Create(contact.Id, definition.Id, "Johnny");
        await _dbContext.ContactCustomFields.AddAsync(field);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContact360Handler(_dbContext, _tenantAccessor, _aggregator, NullLogger<GetContact360Handler>.Instance);
        var result = await handler.Handle(new GetContact360Query(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CustomFields.Should().HaveCount(1);
        result.Value.CustomFields[0].FieldName.Should().Be("Nickname");
    }

    [Fact]
    public async Task Handle_WithModuleContributors_ShouldAggregateModuleSummaries()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var mockContributor = new TestActivityContributor("crm", new ModuleContactSummary(
            "crm", "CRM", new Dictionary<string, object?> { ["deals"] = 5 }));
        var aggregator = new ContactActivityContributorAggregator([mockContributor]);

        // Act
        var handler = new GetContact360Handler(_dbContext, _tenantAccessor, aggregator, NullLogger<GetContact360Handler>.Instance);
        var result = await handler.Handle(new GetContact360Query(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ModuleSummaries.Should().HaveCount(1);
        result.Value.ModuleSummaries[0].ModuleName.Should().Be("crm");
    }

    [Fact]
    public async Task Handle_EmptyContact_ShouldReturnEmptyCollections()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContact360Handler(_dbContext, _tenantAccessor, _aggregator, NullLogger<GetContact360Handler>.Instance);
        var result = await handler.Handle(new GetContact360Query(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Relationships.Should().BeEmpty();
        result.Value.CommunicationPreferences.Should().BeEmpty();
        result.Value.RecentNotes.Should().BeEmpty();
        result.Value.ConsentRecords.Should().BeEmpty();
        result.Value.RecentActivities.Should().BeEmpty();
        result.Value.CustomFields.Should().BeEmpty();
        result.Value.ModuleSummaries.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }

    private sealed class TestActivityContributor(string moduleName, ModuleContactSummary? summary) : IContactActivityContributor
    {
        public string ModuleName => moduleName;
        public Task<ModuleContactSummary?> GetSummaryAsync(Guid contactId, Guid organizationId, CancellationToken ct)
            => Task.FromResult(summary);
    }
}
