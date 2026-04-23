using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class RequestGdprDeleteTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox;
    private readonly IConfigurationResolver _configurationResolver;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public RequestGdprDeleteTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId, _userId);
        _outbox = Substitute.For<IOutbox>();
        _configurationResolver = Substitute.For<IConfigurationResolver>();
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();

        // Default: hard-delete disabled (anonymize path). Per ADR-0025 the handler now
        // consults IConfigurationResolver instead of ITenantConfiguration.
        _configurationResolver
            .GetAsync<bool>(ComplianceKey.GdprHardDeleteEnabled, Arg.Any<CancellationToken>())
            .Returns(false);

        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidContact_ShouldAnonymizeAndSoftDelete()
    {
        var contact = await SeedContact();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _dbContext.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contact.Id);
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be("[REDACTED]");
        updated.LastName.Should().Be("[REDACTED]");
        updated.Email.Should().BeNull();
        updated.Phone.Should().BeNull();
        updated.IsDeleted.Should().BeTrue();
        updated.DeletedAt.Should().NotBeNull();

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<ContactGdprDeletedIntegrationEvent>(e =>
                e.ContactId == contact.Id.Value
                && e.Reason == "User request"
                && e.Mode == "anonymized"
                && e.ErasedByUserId == _userId
                && e.DeletedAtUtc != default),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AnonymizePath_ShouldWriteAuditEntry()
    {
        var contact = await SeedContact();
        var handler = CreateHandler();

        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        var audit = await _dbContext.GdprErasureAudits
            .FirstOrDefaultAsync(a => a.ContactId == contact.Id.Value);
        audit.Should().NotBeNull();
        audit!.Mode.Should().Be("anonymized");
        audit.ErasedByUserId.Should().Be(_userId);
        audit.TenantId.Should().Be(_tenantId);
        audit.ChildCountsJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_HardDeleteFlagEnabled_ShouldEnqueueJobAndNotAnonymizeImmediately()
    {
        var contact = await SeedContact();
        _configurationResolver
            .GetAsync<bool>(ComplianceKey.GdprHardDeleteEnabled, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message!.Key.Should().Be("lockey_contacts_gdpr_erasure_enqueued");

        // Contact is untouched — the job will handle it.
        var stillPresent = await _dbContext.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contact.Id);
        stillPresent.Should().NotBeNull();
        stillPresent!.FirstName.Should().Be("John");
        stillPresent.IsDeleted.Should().BeFalse();

        // No outbox event emitted on the enqueue path.
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<ContactGdprDeletedIntegrationEvent>(), Arg.Any<CancellationToken>());

        // Background job enqueued.
        _backgroundJobClient.Received(1).Create(
            Arg.Any<Hangfire.Common.Job>(),
            Arg.Any<Hangfire.States.IState>());
    }

    [Fact]
    public async Task Handle_ActiveContact_ShouldBeExcludedFromDefaultQueries()
    {
        var contact = await SeedContact();
        var handler = CreateHandler();

        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        var found = await _dbContext.Contacts
            .FirstOrDefaultAsync(c => c.Id == contact.Id);
        found.Should().BeNull();

        var auditRecord = await _dbContext.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contact.Id);
        auditRecord.Should().NotBeNull();
        auditRecord!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(Guid.NewGuid(), "User request"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_MergedContact_ShouldFail()
    {
        var primary = await SeedContact();
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Jane", "Doe", null, "jane@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(secondary);
        await _dbContext.SaveChangesAsync();
        secondary.MarkMerged(primary.Id);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(secondary.Id.Value, "User request"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_gdpr_delete_merged_contact");
    }

    [Fact]
    public async Task Handle_ContactWithConsents_ShouldRevokeAll()
    {
        var contact = await SeedContact();
        var consent1 = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web");
        var consent2 = ConsentRecord.Create(contact.Id, ConsentType.SmsMarketing, true, "App");
        await _dbContext.ConsentRecords.AddRangeAsync(consent1, consent2);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        var consents = await _dbContext.ConsentRecords
            .Where(c => c.ContactId == contact.Id)
            .ToListAsync();
        consents.Should().AllSatisfy(c => c.RevokedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_ContactWithNotes_ShouldRemoveAll()
    {
        var contact = await SeedContact();
        var note = ContactNote.Create(contact.Id, _orgId, Guid.NewGuid(), "Sensitive info");
        await _dbContext.ContactNotes.AddAsync(note);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        var noteCount = await _dbContext.ContactNotes
            .CountAsync(n => n.ContactId == contact.Id);
        noteCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MissingUserContext_ShouldFailWithInvalidUserContext()
    {
        var contact = await SeedContact();

        // Re-seed tenant accessor without a user id — simulates an unauthenticated caller.
        var anonymousAccessor = new TenantContextAccessor();
        anonymousAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var handler = new RequestGdprDeleteHandler(
            _dbContext, anonymousAccessor, _configurationResolver, _outbox, _backgroundJobClient,
            NullLogger<RequestGdprDeleteHandler>.Instance);

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_invalid_user_context");
    }

    [Fact]
    public async Task Handle_DuplicateWithinDebounce_ShouldBeTreatedAsAlreadyProcessed()
    {
        var contact = await SeedContact();
        var handler = CreateHandler();

        // First call completes normally.
        var first = await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        _outbox.ClearReceivedCalls();

        // Second call within the 60s window: short-circuits with the debounce message.
        var second = await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Message!.Key.Should().Be("lockey_contacts_gdpr_erasure_already_processed");

        // No additional outbox event on the debounced call.
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<ContactGdprDeletedIntegrationEvent>(), Arg.Any<CancellationToken>());

        // Only one audit row was written across the two calls.
        (await _dbContext.GdprErasureAudits
            .CountAsync(a => a.ContactId == contact.Id.Value))
            .Should().Be(1);
    }

    [Fact]
    public async Task Handle_AnonymizePath_ShouldWriteChildCountsWithEightKeys()
    {
        var contact = await SeedContact();
        var consent = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web");
        await _dbContext.ConsentRecords.AddAsync(consent);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        var audit = await _dbContext.GdprErasureAudits
            .FirstAsync(a => a.ContactId == contact.Id.Value);
        var json = audit.ChildCountsJson;
        json.Should().Contain("\"addresses\"");
        json.Should().Contain("\"notes\"");
        json.Should().Contain("\"customFields\"");
        json.Should().Contain("\"tags\"");
        json.Should().Contain("\"relationships\"");
        json.Should().Contain("\"communicationPreferences\"");
        json.Should().Contain("\"activities\"");
        json.Should().Contain("\"consentsAnonymized\"");
        json.Should().NotContain("consentsRevoked");
    }

    [Fact]
    public async Task Handle_ContactWithAddresses_ShouldRemoveAll()
    {
        var contact = await SeedContact();
        var address = ContactAddress.Create(contact.Id, AddressType.Home,
            "123 Main St", "Istanbul", "TR", isPrimary: true);
        await _dbContext.ContactAddresses.AddAsync(address);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        var addressCount = await _dbContext.ContactAddresses
            .CountAsync(a => a.ContactId == contact.Id);
        addressCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidTenantContext_ReturnsFailure()
    {
        var badAccessor = new TenantContextAccessor();
        badAccessor.SetTenant("not-a-guid", _orgId.ToString(), _userId.ToString());

        var handler = new RequestGdprDeleteHandler(
            _dbContext, badAccessor, _configurationResolver, _outbox, _backgroundJobClient,
            NullLogger<RequestGdprDeleteHandler>.Instance);

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(Guid.NewGuid(), "User request"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_invalid_tenant_context");
    }

    private RequestGdprDeleteHandler CreateHandler() =>
        new(_dbContext, _tenantAccessor, _configurationResolver, _outbox, _backgroundJobClient,
            NullLogger<RequestGdprDeleteHandler>.Instance);

    private async Task<Contact> SeedContact()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, "john@test.com", "+905551234567", ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();
        return contact;
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId, Guid userId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), userId.ToString());
        return accessor;
    }
}
