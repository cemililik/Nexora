using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Tests.Infrastructure.IntegrationEvents;

/// <summary>
/// Tests the Documents-module inbox handler for <see cref="ContactGdprDeletedIntegrationEvent"/>.
/// Verifies Document unlinking, signature-recipient PII scrubbing, and InboxGuard idempotency.
/// </summary>
public sealed class ContactGdprDeletedIntegrationEventHandlerTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _contactId = Guid.NewGuid();

    public ContactGdprDeletedIntegrationEventHandlerTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, accessor);
    }

    private ContactGdprDeletedIntegrationEventHandler CreateHandler() =>
        new(_dbContext,
            new InboxGuard<DocumentsDbContext>(_dbContext),
            NullLogger<ContactGdprDeletedIntegrationEventHandler>.Instance);

    private ContactGdprDeletedIntegrationEvent CreateEvent() => new()
    {
        TenantId = _tenantId.ToString(),
        ContactId = _contactId,
        Reason = "data subject request",
        Mode = "anonymized",
        DeletedAtUtc = DateTime.UtcNow,
        ErasedByUserId = _userId
    };

    [Fact]
    public async Task HandleAsync_LinkedDocument_IsUnlinked_UnrelatedDocument_Untouched()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "F", _userId);
        await _dbContext.Folders.AddAsync(folder);

        var linkedDoc = Document.Create(
            _tenantId, _orgId, folder.Id, _userId,
            "contact-file.pdf", "application/pdf", 1024, "key/contact-file.pdf",
            linkedEntityId: _contactId, linkedEntityType: "Contact");
        await _dbContext.Documents.AddAsync(linkedDoc);

        var otherContactDoc = Document.Create(
            _tenantId, _orgId, folder.Id, _userId,
            "other-contact.pdf", "application/pdf", 1024, "key/other.pdf",
            linkedEntityId: Guid.NewGuid(), linkedEntityType: "Contact");
        await _dbContext.Documents.AddAsync(otherContactDoc);

        var unlinkedDoc = Document.Create(
            _tenantId, _orgId, folder.Id, _userId,
            "free.pdf", "application/pdf", 1024, "key/free.pdf");
        await _dbContext.Documents.AddAsync(unlinkedDoc);

        var dealDoc = Document.Create(
            _tenantId, _orgId, folder.Id, _userId,
            "deal.pdf", "application/pdf", 1024, "key/deal.pdf",
            linkedEntityId: _contactId, linkedEntityType: "Deal");
        await _dbContext.Documents.AddAsync(dealDoc);

        await _dbContext.SaveChangesAsync();

        // Act
        await CreateHandler().HandleAsync(CreateEvent(), CancellationToken.None);

        // Assert
        var reloadedLinked = await _dbContext.Documents.FindAsync(linkedDoc.Id);
        Assert.Null(reloadedLinked!.LinkedEntityId);
        Assert.Null(reloadedLinked.LinkedEntityType);

        var reloadedOther = await _dbContext.Documents.FindAsync(otherContactDoc.Id);
        Assert.NotNull(reloadedOther!.LinkedEntityId);
        Assert.Equal("Contact", reloadedOther.LinkedEntityType);

        var reloadedUnlinked = await _dbContext.Documents.FindAsync(unlinkedDoc.Id);
        Assert.Null(reloadedUnlinked!.LinkedEntityId);

        // Entity type "Deal" with same id should NOT be touched — we match on type+id.
        var reloadedDeal = await _dbContext.Documents.FindAsync(dealDoc.Id);
        Assert.Equal(_contactId, reloadedDeal!.LinkedEntityId);
        Assert.Equal("Deal", reloadedDeal.LinkedEntityType);
    }

    [Fact]
    public async Task HandleAsync_SignatureRecipientForContact_HasPiiScrubbed()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "F", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "contract.pdf", "application/pdf", 2048, "key/contract.pdf");
        await _dbContext.Documents.AddAsync(doc);

        var request = SignatureRequest.Create(_tenantId, _orgId, doc.Id, _userId, "Sign");
        request.AddRecipient(_contactId, "signer@example.com", "Alice Signer", 1);
        request.AddRecipient(Guid.NewGuid(), "other@example.com", "Bob Other", 2);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        // Simulate that the matching recipient had signed (so IP/Name/Email present).
        var matchingRecipient = request.Recipients.First(r => r.ContactId == _contactId);
        matchingRecipient.Sign("signature-data", "203.0.113.42");
        await _dbContext.SaveChangesAsync();

        // Act
        await CreateHandler().HandleAsync(CreateEvent(), CancellationToken.None);

        // Assert — matching recipient scrubbed
        var scrubbed = await _dbContext.SignatureRecipients.FindAsync(matchingRecipient.Id);
        Assert.Equal("[REDACTED]", scrubbed!.Name);
        Assert.Equal("[REDACTED]", scrubbed.Email);
        Assert.Null(scrubbed.IpAddress);
        Assert.Equal("signature-data", scrubbed.SignatureData); // audit trail retained
        Assert.NotNull(scrubbed.SignedAt);

        // Other recipient untouched
        var otherRecipient = request.Recipients.First(r => r.ContactId != _contactId);
        var reloadedOther = await _dbContext.SignatureRecipients.FindAsync(otherRecipient.Id);
        Assert.Equal("Bob Other", reloadedOther!.Name);
        Assert.Equal("other@example.com", reloadedOther.Email);
    }

    [Fact]
    public async Task HandleAsync_RunTwice_IsIdempotent_SecondRunIsNoOp()
    {
        // Arrange — second run should be blocked by InboxGuard based on EventId.
        var folder = Folder.Create(_tenantId, _orgId, "F", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var linkedDoc = Document.Create(
            _tenantId, _orgId, folder.Id, _userId,
            "contact.pdf", "application/pdf", 1024, "key/contact.pdf",
            linkedEntityId: _contactId, linkedEntityType: "Contact");
        await _dbContext.Documents.AddAsync(linkedDoc);
        await _dbContext.SaveChangesAsync();

        var @event = CreateEvent();

        // Act — first run processes, second run must short-circuit.
        await CreateHandler().HandleAsync(@event, CancellationToken.None);

        // Re-link the document artificially to detect whether the second run mutates it.
        linkedDoc.LinkToEntity(_contactId, "Contact");
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(@event, CancellationToken.None);

        // Assert — second run did NOT unlink again (InboxGuard blocked execution).
        var reloaded = await _dbContext.Documents.FindAsync(linkedDoc.Id);
        Assert.Equal(_contactId, reloaded!.LinkedEntityId);
        Assert.Equal("Contact", reloaded.LinkedEntityType);

        // Inbox has exactly one row for this EventId.
        var inboxCount = await _dbContext.Set<InboxMessage>()
            .CountAsync(m => m.EventId == @event.EventId);
        Assert.Equal(1, inboxCount);
    }

    public void Dispose() => _dbContext.Dispose();
}
