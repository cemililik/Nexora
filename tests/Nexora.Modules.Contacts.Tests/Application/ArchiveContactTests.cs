using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class ArchiveContactTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ArchiveContactTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ActiveContact_ShouldArchive()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new ArchiveContactHandler(_dbContext, _tenantAccessor, NullLogger<ArchiveContactHandler>.Instance);
        var result = await handler.Handle(new ArchiveContactCommand(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var archived = await _dbContext.Contacts.FirstAsync();
        archived.Status.Should().Be(ContactStatus.Archived);
    }

    [Fact]
    public async Task Handle_NonExistentContact_ShouldReturnFailure()
    {
        // Arrange
        var handler = new ArchiveContactHandler(_dbContext, _tenantAccessor, NullLogger<ArchiveContactHandler>.Instance);
        var result = await handler.Handle(new ArchiveContactCommand(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_AlreadyArchived_ShouldReturnFailure()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        contact.Archive();
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new ArchiveContactHandler(_dbContext, _tenantAccessor, NullLogger<ArchiveContactHandler>.Instance);
        var result = await handler.Handle(new ArchiveContactCommand(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_already_archived");
    }

    [Fact]
    public async Task Handle_ShouldPersistArchivedStatus()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Test", "User", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new ArchiveContactHandler(_dbContext, _tenantAccessor, NullLogger<ArchiveContactHandler>.Instance);
        await handler.Handle(new ArchiveContactCommand(contact.Id.Value), CancellationToken.None);

        // Assert
        var persisted = await _dbContext.Contacts.FirstAsync();
        persisted.Status.Should().Be(ContactStatus.Archived);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
