using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetContactNotesTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetContactNotesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithNotes_ShouldReturnPinnedFirst()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var note1 = ContactNote.Create(contact.Id, Guid.NewGuid(), _orgId, "Regular note");
        var note2 = ContactNote.Create(contact.Id, Guid.NewGuid(), _orgId, "Pinned note");
        note2.Pin();
        await _dbContext.ContactNotes.AddRangeAsync(note1, note2);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactNotesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactNotesHandler>.Instance);
        var result = await handler.Handle(new GetContactNotesQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value[0].IsPinned.Should().BeTrue();
        result.Value[0].Content.Should().Be("Pinned note");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new GetContactNotesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactNotesHandler>.Instance);
        var result = await handler.Handle(new GetContactNotesQuery(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoNotes_ShouldReturnEmpty()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactNotesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactNotesHandler>.Instance);
        var result = await handler.Handle(new GetContactNotesQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
