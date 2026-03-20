using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetContactRelationshipsTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetContactRelationshipsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithRelationships_ShouldReturn()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        var related = Contact.Create(_tenantId, _orgId, ContactType.Individual, "Jane", "Smith", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(contact, related);
        await _dbContext.SaveChangesAsync();

        var rel = ContactRelationship.Create(contact.Id, related.Id, RelationshipType.ParentOf);
        await _dbContext.ContactRelationships.AddAsync(rel);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactRelationshipsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactRelationshipsHandler>.Instance);
        var result = await handler.Handle(new GetContactRelationshipsQuery(contact.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].RelatedContactDisplayName.Should().Be("Jane Smith");
        result.Value[0].Type.Should().Be("ParentOf");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new GetContactRelationshipsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactRelationshipsHandler>.Instance);
        var result = await handler.Handle(new GetContactRelationshipsQuery(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoRelationships_ShouldReturnEmpty()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetContactRelationshipsHandler(_dbContext, _tenantAccessor, NullLogger<GetContactRelationshipsHandler>.Instance);
        var result = await handler.Handle(new GetContactRelationshipsQuery(contact.Id.Value), CancellationToken.None);

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
