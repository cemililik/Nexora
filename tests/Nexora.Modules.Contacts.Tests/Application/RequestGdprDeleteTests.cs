using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class RequestGdprDeleteTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public RequestGdprDeleteTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidContact_ShouldAnonymize()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        // Act
        result.IsSuccess.Should().BeTrue();

        // Assert
        var updated = await _dbContext.Contacts.FindAsync(contact.Id);
        updated!.FirstName.Should().Be("[REDACTED]");
        updated.LastName.Should().Be("[REDACTED]");
        updated.Email.Should().BeNull();
        updated.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ActiveContact_ShouldArchive()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = CreateHandler();

        // Act
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        // Assert
        var updated = await _dbContext.Contacts.FindAsync(contact.Id);
        updated!.Status.Should().Be(ContactStatus.Archived);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new RequestGdprDeleteCommand(Guid.NewGuid(), "User request"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_MergedContact_ShouldFail()
    {
        // Arrange
        var primary = await SeedContact();
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Jane", "Doe", null, "jane@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(secondary);
        await _dbContext.SaveChangesAsync();
        secondary.MarkMerged(primary.Id);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new RequestGdprDeleteCommand(secondary.Id.Value, "User request"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_gdpr_delete_merged_contact");
    }

    [Fact]
    public async Task Handle_ContactWithConsents_ShouldRevokeAll()
    {
        // Arrange
        var contact = await SeedContact();
        var consent1 = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web");
        var consent2 = ConsentRecord.Create(contact.Id, ConsentType.SmsMarketing, true, "App");
        await _dbContext.ConsentRecords.AddRangeAsync(consent1, consent2);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        // Assert
        var consents = await _dbContext.ConsentRecords
            .Where(c => c.ContactId == contact.Id)
            .ToListAsync();
        consents.Should().AllSatisfy(c => c.RevokedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_ContactWithNotes_ShouldRemoveAll()
    {
        // Arrange
        var contact = await SeedContact();
        var note = ContactNote.Create(contact.Id, _orgId, Guid.NewGuid(), "Sensitive info");
        await _dbContext.ContactNotes.AddAsync(note);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        // Assert
        var noteCount = await _dbContext.ContactNotes
            .CountAsync(n => n.ContactId == contact.Id);
        noteCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ContactWithAddresses_ShouldRemoveAll()
    {
        // Arrange
        var contact = await SeedContact();
        var address = ContactAddress.Create(contact.Id, AddressType.Home,
            "123 Main St", "Istanbul", "TR", isPrimary: true);
        await _dbContext.ContactAddresses.AddAsync(address);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = CreateHandler();
        await handler.Handle(
            new RequestGdprDeleteCommand(contact.Id.Value, "User request"),
            CancellationToken.None);

        // Assert
        var addressCount = await _dbContext.ContactAddresses
            .CountAsync(a => a.ContactId == contact.Id);
        addressCount.Should().Be(0);
    }

    private RequestGdprDeleteHandler CreateHandler() =>
        new(_dbContext, _tenantAccessor, NullLogger<RequestGdprDeleteHandler>.Instance);

    private async Task<Contact> SeedContact()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "John", "Doe", null, "john@test.com", "+905551234567", ContactSource.Manual);
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
