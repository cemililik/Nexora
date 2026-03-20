using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateTagTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateTagTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingTag_ShouldUpdate()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Old Name", TagCategory.Donor, "#000000");
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new UpdateTagHandler(_dbContext, _tenantAccessor, NullLogger<UpdateTagHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTagCommand(tag.Id.Value, "New Name", "Volunteer", "#FFFFFF"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Name");
        result.Value.Category.Should().Be("Volunteer");
        result.Value.Color.Should().Be("#FFFFFF");
    }

    [Fact]
    public async Task Handle_NonExistentTag_ShouldFail()
    {
        // Arrange
        var handler = new UpdateTagHandler(_dbContext, _tenantAccessor, NullLogger<UpdateTagHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTagCommand(Guid.NewGuid(), "Name", "Donor", null),
            CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_found");
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldFail()
    {
        // Arrange
        var tag1 = Tag.Create(_tenantId, "Tag A", TagCategory.Donor);
        var tag2 = Tag.Create(_tenantId, "Tag B", TagCategory.Volunteer);
        await _dbContext.Tags.AddRangeAsync(tag1, tag2);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new UpdateTagHandler(_dbContext, _tenantAccessor, NullLogger<UpdateTagHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTagCommand(tag2.Id.Value, "Tag A", "Volunteer", null),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_name_duplicate");
    }

    [Fact]
    public async Task Handle_SameNameSameTag_ShouldSucceed()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Keep Name", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new UpdateTagHandler(_dbContext, _tenantAccessor, NullLogger<UpdateTagHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTagCommand(tag.Id.Value, "Keep Name", "Volunteer", "#123456"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Category.Should().Be("Volunteer");
    }

    [Fact]
    public async Task Handle_DifferentTenantTag_ShouldFail()
    {
        // Arrange
        var otherTag = Tag.Create(Guid.NewGuid(), "Other Tenant", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(otherTag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new UpdateTagHandler(_dbContext, _tenantAccessor, NullLogger<UpdateTagHandler>.Instance);
        var result = await handler.Handle(
            new UpdateTagCommand(otherTag.Id.Value, "Updated", "Donor", null),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_found");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
