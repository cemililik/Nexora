using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class RecordConsentTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public RecordConsentTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_GrantConsent_ShouldCreate()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new RecordConsentHandler(_dbContext, _tenantAccessor, NullLogger<RecordConsentHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RecordConsentCommand(contact.Id.Value, "EmailMarketing", true, "Web"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ConsentType.Should().Be("EmailMarketing");
        result.Value.Granted.Should().BeTrue();
        result.Value.Source.Should().Be("Web");
    }

    [Fact]
    public async Task Handle_RevokeConsent_ShouldRevoke()
    {
        // Arrange
        var contact = await SeedContact();
        var consent = ConsentRecord.Create(contact.Id, ConsentType.EmailMarketing, true, "Web");
        await _dbContext.ConsentRecords.AddAsync(consent);
        await _dbContext.SaveChangesAsync();

        var handler = new RecordConsentHandler(_dbContext, _tenantAccessor, NullLogger<RecordConsentHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RecordConsentCommand(contact.Id.Value, "EmailMarketing", false),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Granted.Should().BeFalse();
        result.Value.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_RevokeNoActiveConsent_ShouldFail()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new RecordConsentHandler(_dbContext, _tenantAccessor, NullLogger<RecordConsentHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RecordConsentCommand(contact.Id.Value, "EmailMarketing", false),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_no_active_consent");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new RecordConsentHandler(_dbContext, _tenantAccessor, NullLogger<RecordConsentHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RecordConsentCommand(Guid.NewGuid(), "EmailMarketing", true),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_MultipleConsentsGranted_ShouldAllExist()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new RecordConsentHandler(_dbContext, _tenantAccessor, NullLogger<RecordConsentHandler>.Instance);

        await handler.Handle(
            new RecordConsentCommand(contact.Id.Value, "EmailMarketing", true, "Web"),
            CancellationToken.None);

        // Act
        await handler.Handle(
            new RecordConsentCommand(contact.Id.Value, "SmsMarketing", true, "App"),
            CancellationToken.None);

        // Assert
        var count = await _dbContext.ConsentRecords.CountAsync(c => c.ContactId == contact.Id);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_GrantWithIpAddress_ShouldStore()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new RecordConsentHandler(_dbContext, _tenantAccessor, NullLogger<RecordConsentHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new RecordConsentCommand(contact.Id.Value, "DataProcessing", true, "Form", "192.168.1.1"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ConsentType.Should().Be("DataProcessing");
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
