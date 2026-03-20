using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.Services;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetDuplicateContactsTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetDuplicateContactsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithDuplicates_ShouldReturnSorted()
    {
        // Arrange
        var source = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        var exact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        var partial = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var noMatch = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Alice", "Wonder", null, "alice@other.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(source, exact, partial, noMatch);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetDuplicateContactsHandler(_dbContext, new DuplicateDetectionService(), _tenantAccessor, NullLogger<GetDuplicateContactsHandler>.Instance);
        var result = await handler.Handle(new GetDuplicateContactsQuery(source.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCountGreaterOrEqualTo(1);
        result.Value[0].Score.Should().BeGreaterThan(result.Value.Count > 1 ? result.Value[1].Score : 0);
    }

    [Fact]
    public async Task Handle_NoDuplicates_ShouldReturnEmpty()
    {
        // Arrange
        var source = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        var other = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Alice", "Wonder", null, "alice@other.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(source, other);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetDuplicateContactsHandler(_dbContext, new DuplicateDetectionService(), _tenantAccessor, NullLogger<GetDuplicateContactsHandler>.Instance);
        var result = await handler.Handle(new GetDuplicateContactsQuery(source.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new GetDuplicateContactsHandler(_dbContext, new DuplicateDetectionService(), _tenantAccessor, NullLogger<GetDuplicateContactsHandler>.Instance);
        var result = await handler.Handle(new GetDuplicateContactsQuery(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CustomThreshold_ShouldFilter()
    {
        // Arrange
        var source = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var nameMatch = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(source, nameMatch);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDuplicateContactsHandler(_dbContext, new DuplicateDetectionService(), _tenantAccessor, NullLogger<GetDuplicateContactsHandler>.Instance);

        // Act
        // High threshold should exclude name-only matches (score=25)
        var result = await handler.Handle(new GetDuplicateContactsQuery(source.Id.Value, Threshold: 30), CancellationToken.None);
        result.Value!.Should().BeEmpty();

        // Assert
        // Low threshold should include them
        var result2 = await handler.Handle(new GetDuplicateContactsQuery(source.Id.Value, Threshold: 20), CancellationToken.None);
        result2.Value!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldExcludeArchivedContacts()
    {
        // Arrange
        var source = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        var archived = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        archived.Archive();
        await _dbContext.Contacts.AddRangeAsync(source, archived);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetDuplicateContactsHandler(_dbContext, new DuplicateDetectionService(), _tenantAccessor, NullLogger<GetDuplicateContactsHandler>.Instance);
        var result = await handler.Handle(new GetDuplicateContactsQuery(source.Id.Value), CancellationToken.None);

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
