using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetTagsTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetTagsTests()
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
        var handler = new GetTagsHandler(_dbContext, _tenantAccessor, NullLogger<GetTagsHandler>.Instance);
        var result = await handler.Handle(new GetTagsQuery(), CancellationToken.None);

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithTags_ShouldReturnAll()
    {
        // Arrange
        await SeedTags(3);

        // Act
        var handler = new GetTagsHandler(_dbContext, _tenantAccessor, NullLogger<GetTagsHandler>.Instance);
        var result = await handler.Handle(new GetTagsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_FilterByCategory_ShouldFilter()
    {
        // Arrange
        var donor = Tag.Create(_tenantId, "Donor Tag", TagCategory.Donor);
        var volunteer = Tag.Create(_tenantId, "Volunteer Tag", TagCategory.Volunteer);
        await _dbContext.Tags.AddRangeAsync(donor, volunteer);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetTagsHandler(_dbContext, _tenantAccessor, NullLogger<GetTagsHandler>.Instance);
        var result = await handler.Handle(new GetTagsQuery(Category: "Donor"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].Category.Should().Be("Donor");
    }

    [Fact]
    public async Task Handle_FilterByIsActive_ShouldFilter()
    {
        // Arrange
        var active = Tag.Create(_tenantId, "Active Tag", TagCategory.Donor);
        var inactive = Tag.Create(_tenantId, "Inactive Tag", TagCategory.Donor);
        inactive.Deactivate();
        await _dbContext.Tags.AddRangeAsync(active, inactive);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetTagsHandler(_dbContext, _tenantAccessor, NullLogger<GetTagsHandler>.Instance);
        var result = await handler.Handle(new GetTagsQuery(IsActive: true), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Active Tag");
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentTenant()
    {
        // Arrange
        var ownTag = Tag.Create(_tenantId, "Own Tag", TagCategory.Donor);
        var otherTag = Tag.Create(Guid.NewGuid(), "Other Tenant Tag", TagCategory.Donor);
        await _dbContext.Tags.AddRangeAsync(ownTag, otherTag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetTagsHandler(_dbContext, _tenantAccessor, NullLogger<GetTagsHandler>.Instance);
        var result = await handler.Handle(new GetTagsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Own Tag");
    }

    [Fact]
    public async Task Handle_ShouldOrderByName()
    {
        // Arrange
        var tagC = Tag.Create(_tenantId, "Charlie", TagCategory.Donor);
        var tagA = Tag.Create(_tenantId, "Alpha", TagCategory.Donor);
        var tagB = Tag.Create(_tenantId, "Bravo", TagCategory.Donor);
        await _dbContext.Tags.AddRangeAsync(tagC, tagA, tagB);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetTagsHandler(_dbContext, _tenantAccessor, NullLogger<GetTagsHandler>.Instance);
        var result = await handler.Handle(new GetTagsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value![0].Name.Should().Be("Alpha");
        result.Value[1].Name.Should().Be("Bravo");
        result.Value[2].Name.Should().Be("Charlie");
    }

    private async Task SeedTags(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var tag = Tag.Create(_tenantId, $"Tag{i}", TagCategory.Donor);
            await _dbContext.Tags.AddAsync(tag);
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
