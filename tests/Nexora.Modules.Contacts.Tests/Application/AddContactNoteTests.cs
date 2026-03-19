using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddContactNoteTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public AddContactNoteTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidNote_ShouldCreate()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new AddContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<AddContactNoteHandler>.Instance);
        var authorId = Guid.NewGuid();

        // Act
        var result = await handler.Handle(
            new AddContactNoteCommand(contact.Id.Value, authorId, "Test note content"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("Test note content");
        result.Value.AuthorUserId.Should().Be(authorId);
        result.Value.IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new AddContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<AddContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new AddContactNoteCommand(Guid.NewGuid(), Guid.NewGuid(), "Content"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_ContentTrimmed_ShouldTrim()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new AddContactNoteHandler(_dbContext, _tenantAccessor, NullLogger<AddContactNoteHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new AddContactNoteCommand(contact.Id.Value, Guid.NewGuid(), "  trimmed content  "),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("trimmed content");
    }

    private async Task<Contact> SeedContact()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
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
