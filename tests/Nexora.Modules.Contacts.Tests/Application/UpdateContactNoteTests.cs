using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateContactNoteTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateContactNoteTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ShouldUpdateContent()
    {
        // Arrange
        var (contact, note) = await SeedContactWithNote();
        var handler = new UpdateContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateContactNoteCommand(contact.Id.Value, note.Id.Value, "Updated content"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new UpdateContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateContactNoteCommand(Guid.NewGuid(), Guid.NewGuid(), "Content"),
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

        var handler = new UpdateContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateContactNoteCommand(contact.Id.Value, Guid.NewGuid(), "Content"),
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

        var note = ContactNote.Create(contact.Id, Guid.NewGuid(), _orgId, "Original content");
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
