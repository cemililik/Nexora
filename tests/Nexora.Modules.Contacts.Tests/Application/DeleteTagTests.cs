using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class DeleteTagTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DeleteTagTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ActiveTag_ShouldDeactivate()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "To Deactivate", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteTagHandler(_dbContext, _tenantAccessor, NullLogger<DeleteTagHandler>.Instance);
        var result = await handler.Handle(new DeleteTagCommand(tag.Id.Value), CancellationToken.None);

        // Act
        result.IsSuccess.Should().BeTrue();

        // Assert
        var updated = await _dbContext.Tags.FirstAsync(t => t.Id == tag.Id);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentTag_ShouldFail()
    {
        // Arrange
        var handler = new DeleteTagHandler(_dbContext, _tenantAccessor, NullLogger<DeleteTagHandler>.Instance);
        var result = await handler.Handle(new DeleteTagCommand(Guid.NewGuid()), CancellationToken.None);

        // Act & Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_not_found");
    }

    [Fact]
    public async Task Handle_AlreadyDeactivated_ShouldFail()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Already Inactive", TagCategory.Vendor);
        tag.Deactivate();
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new DeleteTagHandler(_dbContext, _tenantAccessor, NullLogger<DeleteTagHandler>.Instance);
        var result = await handler.Handle(new DeleteTagCommand(tag.Id.Value), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_tag_already_deactivated");
    }

    [Fact]
    public async Task Handle_DifferentTenantTag_ShouldFail()
    {
        // Arrange
        var otherTag = Tag.Create(Guid.NewGuid(), "Other Tenant", TagCategory.Donor);
        await _dbContext.Tags.AddAsync(otherTag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new DeleteTagHandler(_dbContext, _tenantAccessor, NullLogger<DeleteTagHandler>.Instance);
        var result = await handler.Handle(new DeleteTagCommand(otherTag.Id.Value), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessMessage()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Message Tag", TagCategory.Staff);
        await _dbContext.Tags.AddAsync(tag);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new DeleteTagHandler(_dbContext, _tenantAccessor, NullLogger<DeleteTagHandler>.Instance);
        var result = await handler.Handle(new DeleteTagCommand(tag.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message!.Key.Should().Be("lockey_contacts_tag_deactivated");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
