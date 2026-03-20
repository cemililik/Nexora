using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateContactTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateContactTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldUpdateContact()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Old", "Name", null, "old@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateContactHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactHandler>.Instance);
        var command = new UpdateContactCommand(contact.Id.Value, "New", "Name", null, "new@test.com", "+123", null, null, null, "tr", "TRY");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("New Name");
        result.Value.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task Handle_NonExistentContact_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateContactHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactHandler>.Instance);
        var command = new UpdateContactCommand(Guid.NewGuid(), "Test", "User", null, null, null, null, null, null, "en", "USD");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_ShouldUpdatePhone()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Test", "User", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateContactHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactHandler>.Instance);
        var command = new UpdateContactCommand(contact.Id.Value, "Test", "User", null, null, "+905551234567", "+905559876543", null, null, "en", "USD");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Phone.Should().Be("+905551234567");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        var contact = Contact.Create(otherTenantId, _orgId, ContactType.Individual, "Other", "Tenant", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateContactHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactHandler>.Instance);
        var command = new UpdateContactCommand(contact.Id.Value, "Test", "User", null, null, null, null, null, null, "en", "USD");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPersistChanges()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Old", "Name", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new UpdateContactHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactHandler>.Instance);
        await handler.Handle(new UpdateContactCommand(contact.Id.Value, "Updated", "Name", null, null, null, null, null, null, "en", "USD"), CancellationToken.None);

        // Assert
        var updated = await _dbContext.Contacts.FirstAsync();
        updated.FirstName.Should().Be("Updated");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
