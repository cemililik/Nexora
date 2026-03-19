using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddContactAddressTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public AddContactAddressTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidAddress_ShouldAdd()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new AddContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<AddContactAddressHandler>.Instance);

        // Act
        var result = await handler.Handle(new AddContactAddressCommand(
            contact.Id.Value, "Home", "123 Main St", "Istanbul", "TR"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Street1.Should().Be("123 Main St");
        result.Value.City.Should().Be("Istanbul");
        result.Value.CountryCode.Should().Be("TR");
        result.Value.Type.Should().Be("Home");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new AddContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<AddContactAddressHandler>.Instance);
        var result = await handler.Handle(new AddContactAddressCommand(
            Guid.NewGuid(), "Home", "St", "City", "TR"), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_PrimaryAddress_ShouldUnsetExistingPrimary()
    {
        // Arrange
        var contact = await SeedContact();
        var existing = ContactAddress.Create(contact.Id, AddressType.Home, "Old St", "Old City", "TR", isPrimary: true);
        await _dbContext.ContactAddresses.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new AddContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<AddContactAddressHandler>.Instance);
        await handler.Handle(new AddContactAddressCommand(
            contact.Id.Value, "Work", "New St", "New City", "TR", IsPrimary: true), CancellationToken.None);

        // Assert
        var addresses = await _dbContext.ContactAddresses.Where(a => a.ContactId == contact.Id).ToListAsync();
        addresses.Count(a => a.IsPrimary).Should().Be(1);
        addresses.Single(a => a.IsPrimary).Street1.Should().Be("New St");
    }

    [Fact]
    public async Task Handle_ShouldNormalizeCountryCode()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new AddContactAddressHandler(_dbContext, _tenantAccessor, NullLogger<AddContactAddressHandler>.Instance);

        // Act
        var result = await handler.Handle(new AddContactAddressCommand(
            contact.Id.Value, "Home", "St", "City", "tr"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CountryCode.Should().Be("TR");
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
