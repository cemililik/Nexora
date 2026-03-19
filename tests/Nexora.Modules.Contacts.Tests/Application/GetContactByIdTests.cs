using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetContactByIdTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetContactByIdTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingContact_ShouldReturnDetail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", "+123", ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetContactByIdHandler>.Instance);
        var result = await handler.Handle(new GetContactByIdQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("John Doe");
        result.Value.Email.Should().Be("john@test.com");
        result.Value.Addresses.Should().BeEmpty();
        result.Value.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonExistentContact_ShouldReturnFailure()
    {
        // Arrange
        var handler = new GetContactByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetContactByIdHandler>.Instance);
        var result = await handler.Handle(new GetContactByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_WithAddresses_ShouldIncludeAddresses()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var address = ContactAddress.Create(contact.Id, AddressType.Home, "123 Main St", "Istanbul", "TR");
        await _dbContext.ContactAddresses.AddAsync(address);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetContactByIdHandler>.Instance);
        var result = await handler.Handle(new GetContactByIdQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Addresses.Should().HaveCount(1);
        result.Value.Addresses[0].City.Should().Be("Istanbul");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange
        var otherTenant = Guid.NewGuid();
        var contact = Contact.Create(otherTenant, _orgId, ContactType.Individual, "Other", "Tenant", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetContactByIdHandler>.Instance);
        var result = await handler.Handle(new GetContactByIdQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
