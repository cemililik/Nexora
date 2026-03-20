using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateCommunicationPreferencesTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateCommunicationPreferencesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NewPreferences_ShouldCreate()
    {
        // Arrange
        var contact = await SeedContact();
        var handler = new UpdateCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCommunicationPreferencesHandler>.Instance);

        var prefs = new List<ChannelPreference>
        {
            new("Email", true, "Registration"),
            new("Sms", false)
        };

        var result = await handler.Handle(
            new UpdateCommunicationPreferencesCommand(contact.Id.Value, prefs),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);

        var email = result.Value.First(p => p.Channel == "Email");
        email.OptedIn.Should().BeTrue();
        email.OptInSource.Should().Be("Registration");

        var sms = result.Value.First(p => p.Channel == "Sms");
        sms.OptedIn.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExistingPreference_ShouldUpdate()
    {
        var contact = await SeedContact();
        var existing = CommunicationPreference.Create(contact.Id, CommunicationChannel.Email, true, "Initial");
        await _dbContext.CommunicationPreferences.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCommunicationPreferencesHandler>.Instance);
        var result = await handler.Handle(
            new UpdateCommunicationPreferencesCommand(contact.Id.Value, [new("Email", false)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].OptedIn.Should().BeFalse();

        var count = await _dbContext.CommunicationPreferences.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        var handler = new UpdateCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCommunicationPreferencesHandler>.Instance);
        var result = await handler.Handle(
            new UpdateCommunicationPreferencesCommand(Guid.NewGuid(), [new("Email", true)]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_OptInAfterOptOut_ShouldUpdateTimestamps()
    {
        var contact = await SeedContact();
        var existing = CommunicationPreference.Create(contact.Id, CommunicationChannel.Sms, false);
        await _dbContext.CommunicationPreferences.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCommunicationPreferencesHandler>.Instance);
        var result = await handler.Handle(
            new UpdateCommunicationPreferencesCommand(contact.Id.Value, [new("Sms", true, "Re-consent")]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].OptedIn.Should().BeTrue();
        result.Value[0].OptInSource.Should().Be("Re-consent");
        result.Value[0].OptedInAt.Should().NotBeNull();
    }

    private async Task<Contact> SeedContact()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();
        return contact;
    }

        // Act
    public void Dispose() => _dbContext.Dispose();

        // Assert
    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
