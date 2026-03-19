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
