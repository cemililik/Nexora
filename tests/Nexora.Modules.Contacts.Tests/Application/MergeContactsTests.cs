using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class MergeContactsTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public MergeContactsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidMerge_ShouldSucceed()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var handler = new MergeContactsHandler(_dbContext, _tenantAccessor, NullLogger<MergeContactsHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new MergeContactsCommand(primary.Id.Value, secondary.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.PrimaryContactId.Should().Be(primary.Id.Value);
        result.Value.SecondaryContactId.Should().Be(secondary.Id.Value);
    }

    [Fact]
    public async Task Handle_PrimaryNotFound_ShouldFail()
    {
        // Arrange
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(secondary);
        await _dbContext.SaveChangesAsync();

        var handler = new MergeContactsHandler(_dbContext, _tenantAccessor, NullLogger<MergeContactsHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new MergeContactsCommand(Guid.NewGuid(), secondary.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_primary_contact_not_found");
    }

    [Fact]
    public async Task Handle_SecondaryNotFound_ShouldFail()
    {
        // Arrange
        var primary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(primary);
        await _dbContext.SaveChangesAsync();

        var handler = new MergeContactsHandler(_dbContext, _tenantAccessor, NullLogger<MergeContactsHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new MergeContactsCommand(primary.Id.Value, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_secondary_contact_not_found");
    }

    [Fact]
    public async Task Handle_ArchivedPrimary_ShouldFail()
    {
        // Arrange
        var primary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        primary.Archive();
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(primary, secondary);
        await _dbContext.SaveChangesAsync();

        var handler = new MergeContactsHandler(_dbContext, _tenantAccessor, NullLogger<MergeContactsHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new MergeContactsCommand(primary.Id.Value, secondary.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MergedSecondaryStatus_ShouldBeMerged()
    {
        // Arrange
        var (primary, secondary) = await SeedTwoContacts();
        var handler = new MergeContactsHandler(_dbContext, _tenantAccessor, NullLogger<MergeContactsHandler>.Instance);

        // Act
        await handler.Handle(
            new MergeContactsCommand(primary.Id.Value, secondary.Id.Value),
            CancellationToken.None);

        // Assert
        var updatedSecondary = await _dbContext.Contacts.FirstAsync(c => c.Id == secondary.Id);
        updatedSecondary.Status.Should().Be(ContactStatus.Merged);
        updatedSecondary.MergedIntoId.Should().Be(primary.Id);
    }

    [Fact]
    public async Task Handle_WithFieldSelections_ShouldApply()
    {
        // Arrange
        var primary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@old.com", null, ContactSource.Manual);
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, "jane@new.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(primary, secondary);
        await _dbContext.SaveChangesAsync();

        var handler = new MergeContactsHandler(_dbContext, _tenantAccessor, NullLogger<MergeContactsHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new MergeContactsCommand(primary.Id.Value, secondary.Id.Value, UseSecondaryEmail: true),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        primary.Email.Should().Be("jane@new.com");
    }

    private async Task<(Contact primary, Contact secondary)> SeedTwoContacts()
    {
        var primary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var secondary = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(primary, secondary);
        await _dbContext.SaveChangesAsync();
        return (primary, secondary);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
