using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class PinContactNoteTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public PinContactNoteTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_PinNote_ShouldSetPinned()
    {
        // Arrange
        var (contact, note) = await SeedContactWithNote();
        var handler = new PinContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<PinContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new PinContactNoteCommand(contact.Id.Value, note.Id.Value, true),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnpinNote_ShouldUnsetPinned()
    {
        // Arrange
        var (contact, note) = await SeedContactWithNote();
        note.Pin();
        await _dbContext.SaveChangesAsync();

        var handler = new PinContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<PinContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new PinContactNoteCommand(contact.Id.Value, note.Id.Value, false),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new PinContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<PinContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new PinContactNoteCommand(Guid.NewGuid(), Guid.NewGuid(), true),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_NoteNotFound_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var handler = new PinContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<PinContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new PinContactNoteCommand(contact.Id.Value, Guid.NewGuid(), true),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_note_not_found");
    }

    private async Task<(Contact contact, ContactNote note)> SeedContactWithNote()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var note = ContactNote.Create(contact.Id, Guid.NewGuid(), _orgId, "Test note");
        await _dbContext.ContactNotes.AddAsync(note);
        await _dbContext.SaveChangesAsync();
        return (contact, note);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
