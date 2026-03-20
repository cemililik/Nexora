using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetCommunicationPreferencesTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetCommunicationPreferencesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithPreferences_ShouldReturn()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var emailPref = CommunicationPreference.Create(contact.Id, CommunicationChannel.Email, true, "Web");
        var smsPref = CommunicationPreference.Create(contact.Id, CommunicationChannel.Sms, false);
        await _dbContext.CommunicationPreferences.AddRangeAsync(emailPref, smsPref);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<GetCommunicationPreferencesHandler>.Instance);
        var result = await handler.Handle(new GetCommunicationPreferencesQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new GetCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<GetCommunicationPreferencesHandler>.Instance);
        var result = await handler.Handle(new GetCommunicationPreferencesQuery(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoPreferences_ShouldReturnEmpty()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetCommunicationPreferencesHandler(_dbContext, _tenantAccessor, NullLogger<GetCommunicationPreferencesHandler>.Instance);
        var result = await handler.Handle(new GetCommunicationPreferencesQuery(contact.Id.Value), CancellationToken.None);

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
