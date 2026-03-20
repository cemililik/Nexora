using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateContactAddressTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateContactAddressTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingAddress_ShouldUpdate()
    {
        // Arrange
        var (contact, address) = await SeedContactWithAddress();
        var handler = new UpdateContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactAddressHandler>.Instance);

        // Act
        var result = await handler.Handle(new UpdateContactAddressCommand(
            contact.Id.Value, address.Id.Value, "Work", "456 New St", "Ankara", "TR"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Street1.Should().Be("456 New St");
        result.Value.City.Should().Be("Ankara");
        result.Value.Type.Should().Be("Work");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new UpdateContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactAddressHandler>.Instance);
        var result = await handler.Handle(new UpdateContactAddressCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Home", "St", "City", "TR"), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_AddressNotFound_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new UpdateContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<UpdateContactAddressHandler>.Instance);
        var result = await handler.Handle(new UpdateContactAddressCommand(
            contact.Id.Value, Guid.NewGuid(), "Home", "St", "City", "TR"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_address_not_found");
    }

    private async Task<(Contact contact, ContactAddress address)> SeedContactWithAddress()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var address = ContactAddress.Create(contact.Id, AddressType.Home, "123 Main St", "Istanbul", "TR");
        await _dbContext.ContactAddresses.AddAsync(address);
        await _dbContext.SaveChangesAsync();
        return (contact, address);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
