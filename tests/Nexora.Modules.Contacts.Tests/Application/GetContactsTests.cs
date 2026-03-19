using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetContactsTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetContactsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        var handler = new GetContactsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactsHandler>.Instance);
        var result = await handler.Handle(new GetContactsQuery(), CancellationToken.None);

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithContacts_ShouldReturnPagedResult()
    {
        // Arrange
        await SeedContacts(3);

        // Act
        var handler = new GetContactsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactsHandler>.Instance);
        var result = await handler.Handle(new GetContactsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FilterByStatus_ShouldFilterCorrectly()
    {
        // Arrange
        var active = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Active", "User", null, null, null, ContactSource.Manual);
        var archived = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Archived", "User", null, null, null, ContactSource.Manual);
        archived.Archive();
        await _dbContext.Contacts.AddRangeAsync(active, archived);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactsHandler>.Instance);
        var result = await handler.Handle(new GetContactsQuery(Status: "Active"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].DisplayName.Should().Be("Active User");
    }

    [Fact]
    public async Task Handle_FilterByType_ShouldFilterCorrectly()
    {
        // Arrange
        var individual = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var org = Contact.Create(_tenantId, _orgId, ContactType.Organization, null, null, "Acme", null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(individual, org);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactsHandler>.Instance);
        var result = await handler.Handle(new GetContactsQuery(Type: "Organization"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Type.Should().Be("Organization");
    }

    [Fact]
    public async Task Handle_SearchByName_ShouldFilterCorrectly()
    {
        // Arrange
        var john = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Smith", null, null, null, ContactSource.Manual);
        var jane = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(john, jane);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactsHandler>.Instance);
        var result = await handler.Handle(new GetContactsQuery(Search: "john"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].FirstName.Should().Be("John");
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentTenant()
    {
        // Arrange
        var ownContact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Own", "Contact", null, null, null, ContactSource.Manual);
        var otherContact = Contact.Create(Guid.NewGuid(), _orgId, ContactType.Individual, "Other", "Tenant", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(ownContact, otherContact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactsHandler>.Instance);
        var result = await handler.Handle(new GetContactsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    private async Task SeedContacts(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, $"Contact{i}", "Test", null, null, null, ContactSource.Manual);
            await _dbContext.Contacts.AddAsync(contact);
        }
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
