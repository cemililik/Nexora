using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Infrastructure;

public sealed class ContactQueryServiceTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ContactQueryService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactQueryServiceTests()
    {
        var tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, tenantAccessor);
        _service = new ContactQueryService(_dbContext, tenantAccessor, new PassThroughCacheService());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingContact_ShouldReturn()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(contact.Id.Value);

        // Assert
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("John Doe");
        result.Email.Should().Be("john@test.com");
        result.Type.Should().Be("Individual");
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingContact_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdsAsync_MultipleContacts_ShouldReturnAll()
    {
        // Arrange
        var c1 = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var c2 = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(c1, c2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdsAsync([c1.Id.Value, c2.Id.Value]);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_ByName_ShouldFindMatches()
    {
        // Arrange
        var c1 = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var c2 = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(c1, c2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SearchAsync("John");

        // Assert
        result.Should().HaveCount(1);
        result[0].DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public async Task SearchAsync_ByEmail_ShouldFindMatches()
    {
        // Arrange
        var c1 = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(c1);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SearchAsync("john@test");

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_MoreResultsThanMax_ShouldRespectMaxResults()
    {
        // Arrange
        for (var i = 0; i < 15; i++)
        {
            var c = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", $"Doe{i}", null, null, null, ContactSource.Manual);
            await _dbContext.Contacts.AddAsync(c);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SearchAsync("John", maxResults: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    public void Dispose() => _dbContext.Dispose();

    private sealed class PassThroughCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CacheOptions? options = null, CancellationToken ct = default) => factory(ct);
        public Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
    }
}
