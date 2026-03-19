using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class LogContactActivityTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public LogContactActivityTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidActivity_ShouldLog()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new LogContactActivityHandler(_dbContext, _tenantAccessor, NullLogger<LogContactActivityHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new LogContactActivityCommand(contact.Id.Value, "contacts", "NoteAdded", "Note added to contact"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ModuleSource.Should().Be("contacts");
        result.Value.ActivityType.Should().Be("NoteAdded");
        result.Value.Summary.Should().Be("Note added to contact");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new LogContactActivityHandler(_dbContext, _tenantAccessor, NullLogger<LogContactActivityHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new LogContactActivityCommand(Guid.NewGuid(), "contacts", "NoteAdded", "Summary"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_WithDetails_ShouldStoreDetails()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new LogContactActivityHandler(_dbContext, _tenantAccessor, NullLogger<LogContactActivityHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new LogContactActivityCommand(contact.Id.Value, "donations", "DonationReceived", "Donation of $100", "Receipt #12345"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Details.Should().Be("Receipt #12345");
        result.Value.ModuleSource.Should().Be("donations");
    }

    [Fact]
    public async Task Handle_AppendOnly_ShouldAccumulateActivities()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new LogContactActivityHandler(_dbContext, _tenantAccessor, NullLogger<LogContactActivityHandler>.Instance);

        await handler.Handle(
            new LogContactActivityCommand(contact.Id.Value, "contacts", "Created", "Contact created"),
            CancellationToken.None);

        // Act
        await handler.Handle(
            new LogContactActivityCommand(contact.Id.Value, "contacts", "Updated", "Contact updated"),
            CancellationToken.None);

        // Assert
        var count = await _dbContext.ContactActivities.CountAsync(a => a.ContactId == contact.Id);
        count.Should().Be(2);
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
