using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class RequestGdprExportTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public RequestGdprExportTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidContact_ShouldReturnExport()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new RequestGdprExportHandler(
            _dbContext, _tenantAccessor, NullLogger<RequestGdprExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RequestGdprExportCommand(contact.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContactId.Should().Be(contact.Id.Value);
        result.Value.DisplayName.Should().Be("John Doe");
        result.Value.ExportedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new RequestGdprExportHandler(
            _dbContext, _tenantAccessor, NullLogger<RequestGdprExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RequestGdprExportCommand(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_ContactWithNotes_ShouldIncludeNotes()
    {
        // Arrange
        var contact = await SeedContact();
        var note = ContactNote.Create(contact.Id, _orgId, Guid.NewGuid(), "Test note content");
        await _dbContext.ContactNotes.AddAsync(note);
        await _dbContext.SaveChangesAsync();

        var handler = new RequestGdprExportHandler(
            _dbContext, _tenantAccessor, NullLogger<RequestGdprExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RequestGdprExportCommand(contact.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Notes.Should().HaveCount(1);
        result.Value.Notes[0].Content.Should().Be("Test note content");
    }

    [Fact]
    public async Task Handle_ContactWithConsents_ShouldIncludeConsents()
    {
        // Arrange
        var contact = await SeedContact();
        var consent = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web");
        await _dbContext.ConsentRecords.AddAsync(consent);
        await _dbContext.SaveChangesAsync();

        var handler = new RequestGdprExportHandler(
            _dbContext, _tenantAccessor, NullLogger<RequestGdprExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RequestGdprExportCommand(contact.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ConsentRecords.Should().HaveCount(1);
        result.Value.ConsentRecords[0].ConsentType.Should().Be("EmailMarketing");
    }

    [Fact]
    public async Task Handle_ContactIsInboundTargetOfRelationship_ShouldReturnCounterPartyDisplayName()
    {
        // Arrange — the subject is the RelatedContactId (inbound side).
        // A naive one-way join on RelatedContactId would return the subject's own
        // DisplayName ("John Doe") instead of the counter-party's — this test guards
        // against that regression.
        var subject = await SeedContact();

        var other = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Alice", "Smith", null, "alice@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(other);
        await _dbContext.SaveChangesAsync();

        // Relationship: other -> subject (subject is the target / RelatedContactId).
        var inbound = ContactRelationship.Create(other.Id, subject.Id, RelationshipType.ContactOf);
        await _dbContext.ContactRelationships.AddAsync(inbound);
        await _dbContext.SaveChangesAsync();

        var handler = new RequestGdprExportHandler(
            _dbContext, _tenantAccessor, NullLogger<RequestGdprExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RequestGdprExportCommand(subject.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Relationships.Should().HaveCount(1);
        var dto = result.Value.Relationships[0];
        dto.RelatedContactDisplayName.Should().Be("Alice Smith",
            "export must return the counter-party's DisplayName, never the subject's own");
        dto.ContactId.Should().Be(other.Id.Value);
        dto.RelatedContactId.Should().Be(subject.Id.Value);
    }

    [Fact]
    public async Task Handle_ContactWithActivities_ShouldIncludeActivities()
    {
        // Arrange
        var contact = await SeedContact();
        var activity = ContactActivity.Create(contact.Id, _orgId, "contacts", "Updated", "Profile updated");
        await _dbContext.ContactActivities.AddAsync(activity);
        await _dbContext.SaveChangesAsync();

        var handler = new RequestGdprExportHandler(
            _dbContext, _tenantAccessor, NullLogger<RequestGdprExportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RequestGdprExportCommand(contact.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Activities.Should().HaveCount(1);
    }

    private async Task<Contact> SeedContact()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();
        return contact;
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
