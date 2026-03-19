using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class CreateTagTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateTagTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidTag_ShouldCreateTag()
    {
        // Arrange
        var handler = new CreateTagHandler(_dbContext, _tenantAccessor, NullLogger<CreateTagHandler>.Instance);
        var command = new CreateTagCommand("VIP Donor", "Donor", "#FF0000");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("VIP Donor");
        result.Value.Category.Should().Be("Donor");
        result.Value.Color.Should().Be("#FF0000");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var handler = new CreateTagHandler(_dbContext, _tenantAccessor, NullLogger<CreateTagHandler>.Instance);
        await handler.Handle(new CreateTagCommand("Test Tag", "Volunteer"), CancellationToken.None);

        // Act
        var count = await _dbContext.Tags.CountAsync();
        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldFail()
    {
        // Arrange
        var existing = Tag.Create(_tenantId, "Duplicate", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new CreateTagHandler(_dbContext, _tenantAccessor, NullLogger<CreateTagHandler>.Instance);
        var result = await handler.Handle(new CreateTagCommand("Duplicate", "Volunteer"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_name_duplicate");
    }

    [Fact]
    public async Task Handle_SameNameDifferentTenant_ShouldSucceed()
    {
        // Arrange
        var otherTenantTag = Tag.Create(Guid.NewGuid(), "Shared Name", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(otherTenantTag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new CreateTagHandler(_dbContext, _tenantAccessor, NullLogger<CreateTagHandler>.Instance);
        var result = await handler.Handle(new CreateTagCommand("Shared Name", "Donor"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutColor_ShouldCreateWithNullColor()
    {
        // Arrange
        var handler = new CreateTagHandler(_dbContext, _tenantAccessor, NullLogger<CreateTagHandler>.Instance);
        var result = await handler.Handle(new CreateTagCommand("No Color", "Staff"), CancellationToken.None);

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Color.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessMessage()
    {
        // Arrange
        var handler = new CreateTagHandler(_dbContext, _tenantAccessor, NullLogger<CreateTagHandler>.Instance);
        var result = await handler.Handle(new CreateTagCommand("Message Test", "Parent"), CancellationToken.None);

        // Act & Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNull();
        result.Message!.Key.Should().Be("lockey_contacts_tag_created");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
