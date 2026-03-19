using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetContactActivitiesTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetContactActivitiesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithActivities_ShouldReturn()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var activity1 = ContactActivity.Create(contact.Id, _orgId, "contacts", "Created", "Contact created");
        var activity2 = ContactActivity.Create(contact.Id, _orgId, "donations", "DonationReceived", "Donation received");
        await _dbContext.ContactActivities.AddRangeAsync(activity1, activity2);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactActivitiesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactActivitiesHandler>.Instance);
        var result = await handler.Handle(new GetContactActivitiesQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByModuleSource_ShouldFilterCorrectly()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var activity1 = ContactActivity.Create(contact.Id, _orgId, "contacts", "Created", "Contact created");
        var activity2 = ContactActivity.Create(contact.Id, _orgId, "donations", "DonationReceived", "Donation received");
        await _dbContext.ContactActivities.AddRangeAsync(activity1, activity2);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactActivitiesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactActivitiesHandler>.Instance);
        var result = await handler.Handle(
            new GetContactActivitiesQuery(contact.Id.Value, ModuleSource: "contacts"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].ModuleSource.Should().Be("contacts");
    }

    [Fact]
    public async Task Handle_WithTakeLimit_ShouldLimitResults()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        for (var i = 0; i < 5; i++)
        {
            var activity = ContactActivity.Create(contact.Id, _orgId, "contacts", $"Type{i}", $"Summary {i}");
            await _dbContext.ContactActivities.AddAsync(activity);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactActivitiesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactActivitiesHandler>.Instance);
        var result = await handler.Handle(
            new GetContactActivitiesQuery(contact.Id.Value, Take: 3),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new GetContactActivitiesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactActivitiesHandler>.Instance);
        var result = await handler.Handle(new GetContactActivitiesQuery(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoActivities_ShouldReturnEmpty()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactActivitiesHandler(_dbContext, _tenantAccessor, NullLogger<GetContactActivitiesHandler>.Instance);
        var result = await handler.Handle(new GetContactActivitiesQuery(contact.Id.Value), CancellationToken.None);

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
