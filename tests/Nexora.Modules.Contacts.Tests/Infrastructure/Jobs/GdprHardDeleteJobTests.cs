using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Domain.Exceptions;
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Infrastructure.Jobs;

/// <summary>
/// Tests for <see cref="GdprHardDeleteJob"/>. Uses EF Core InMemory provider via the
/// provider-agnostic fallback path inside the job (change-tracker delete + row anonymize);
/// the relational path (ExecuteDeleteAsync/ExecuteUpdateAsync) is exercised in integration
/// environments against PostgreSQL.
/// </summary>
public sealed class GdprHardDeleteJobTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _erasedBy = Guid.NewGuid();

    public GdprHardDeleteJobTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        _outbox = Substitute.For<IOutbox>();
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task ExecuteAsync_ValidContact_HardDeletesContactAndChildren_AnonymizesConsents_EmitsEvent()
    {
        // Arrange — seed a contact with all child types.
        var contact = await SeedFullContact();

        var job = CreateJob();

        // Act
        await job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantId.ToString(),
            OrganizationId = _orgId.ToString(),
            ContactId = contact.Id.Value,
            Reason = "Data subject request",
            ErasedByUserId = _erasedBy
        }, CancellationToken.None);

        // Assert — contact is gone (even when ignoring soft-delete filter).
        var contactAfter = await _dbContext.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contact.Id);
        contactAfter.Should().BeNull();

        // All 7 child entity types removed.
        (await _dbContext.ContactAddresses.IgnoreQueryFilters()
            .CountAsync(a => a.ContactId == contact.Id)).Should().Be(0);
        (await _dbContext.ContactNotes.IgnoreQueryFilters()
            .CountAsync(n => n.ContactId == contact.Id)).Should().Be(0);
        (await _dbContext.ContactCustomFields.IgnoreQueryFilters()
            .CountAsync(f => f.ContactId == contact.Id)).Should().Be(0);
        (await _dbContext.ContactTags.IgnoreQueryFilters()
            .CountAsync(t => t.ContactId == contact.Id)).Should().Be(0);
        (await _dbContext.ContactRelationships.IgnoreQueryFilters()
            .CountAsync(r => r.ContactId == contact.Id || r.RelatedContactId == contact.Id))
            .Should().Be(0);
        (await _dbContext.CommunicationPreferences.IgnoreQueryFilters()
            .CountAsync(p => p.ContactId == contact.Id)).Should().Be(0);
        (await _dbContext.ContactActivities.IgnoreQueryFilters()
            .CountAsync(a => a.ContactId == contact.Id)).Should().Be(0);

        // Consent rows preserved but anonymized (Article 17(3)(e)).
        var consents = await _dbContext.ConsentRecords
            .IgnoreQueryFilters()
            .Where(c => c.ContactId == contact.Id)
            .ToListAsync();
        consents.Should().HaveCount(1);
        consents[0].IpAddress.Should().BeNull();
        consents[0].Source.Should().Be("[REDACTED]");

        // Audit record written.
        var audit = await _dbContext.GdprErasureAudits
            .FirstOrDefaultAsync(a => a.ContactId == contact.Id.Value);
        audit.Should().NotBeNull();
        audit!.Mode.Should().Be("hard_deleted");
        audit.TenantId.Should().Be(_tenantId);
        audit.ErasedByUserId.Should().Be(_erasedBy);
        audit.Reason.Should().Be("Data subject request");
        audit.ChildCountsJson.Should().Contain("\"addresses\"");

        // Outbox event emitted with all required fields.
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<ContactGdprDeletedIntegrationEvent>(e =>
                e.ContactId == contact.Id.Value
                && e.Mode == "hard_deleted"
                && e.ErasedByUserId == _erasedBy
                && e.Reason == "Data subject request"
                && e.TenantId == _tenantId.ToString()
                && e.DeletedAtUtc != default),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ContactNotFound_ShouldBeIdempotentAndSkip()
    {
        var job = CreateJob();

        await job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantId.ToString(),
            ContactId = Guid.NewGuid(),
            Reason = "Re-run",
            ErasedByUserId = _erasedBy
        }, CancellationToken.None);

        // No audit, no event.
        (await _dbContext.GdprErasureAudits.CountAsync()).Should().Be(0);
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<ContactGdprDeletedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MergedContact_ShouldThrowDomainException()
    {
        var primary = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Primary", "Keeper", null, "primary@test.com", null, ContactSource.Manual);
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Jane", "Doe", null, "jane@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(primary, secondary);
        await _dbContext.SaveChangesAsync();
        secondary.MarkMerged(primary.Id);
        await _dbContext.SaveChangesAsync();

        var job = CreateJob();

        var act = () => job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantId.ToString(),
            ContactId = secondary.Id.Value,
            Reason = "Should fail",
            ErasedByUserId = _erasedBy
        }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("lockey_contacts_error_gdpr_delete_merged_contact");
    }

    private GdprHardDeleteJob CreateJob() =>
        new(_tenantAccessor, _dbContext, _outbox,
            NullLogger<GdprHardDeleteJob>.Instance);

    private async Task<Contact> SeedFullContact()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, "john@test.com", "+905551234567", ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Address
        var address = ContactAddress.Create(contact.Id, AddressType.Home,
            "123 Main St", "Istanbul", "TR", isPrimary: true);
        await _dbContext.ContactAddresses.AddAsync(address);

        // Note
        var note = ContactNote.Create(contact.Id, _orgId, Guid.NewGuid(), "Sensitive info");
        await _dbContext.ContactNotes.AddAsync(note);

        // Custom field
        var fieldDefId = CustomFieldDefinitionId.New();
        var customField = ContactCustomField.Create(contact.Id, fieldDefId, "some-value");
        await _dbContext.ContactCustomFields.AddAsync(customField);

        // Tag (join row). Use a synthetic TagId — we don't need the Tag row itself for this test.
        var tagId = TagId.New();
        var contactTag = ContactTag.Create(contact.Id, tagId, _orgId);
        await _dbContext.ContactTags.AddAsync(contactTag);

        // Relationship (bidirectional: contact as source and as target).
        var other = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Friend", "Of", null, "friend@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(other);
        await _dbContext.SaveChangesAsync();

        var rel1 = ContactRelationship.Create(contact.Id, other.Id, RelationshipType.ContactOf);
        var rel2 = ContactRelationship.Create(other.Id, contact.Id, RelationshipType.ContactOf);
        await _dbContext.ContactRelationships.AddRangeAsync(rel1, rel2);

        // Communication preference
        var pref = CommunicationPreference.Create(contact.Id, CommunicationChannel.Email, true, "Web");
        await _dbContext.CommunicationPreferences.AddAsync(pref);

        // Activity
        var activity = ContactActivity.Create(contact.Id, _orgId, "contacts", "note_added", "Added a note");
        await _dbContext.ContactActivities.AddAsync(activity);

        // Consent (should be anonymized, not deleted)
        var consent = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web", "192.0.2.1");
        await _dbContext.ConsentRecords.AddAsync(consent);

        await _dbContext.SaveChangesAsync();
        return contact;
    }

    public void Dispose() => _dbContext.Dispose();
}
