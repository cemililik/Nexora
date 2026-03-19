using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class DeleteCustomFieldDefinitionTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DeleteCustomFieldDefinitionTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidDelete_ShouldDeactivate()
    {
        // Arrange
        var definition = CustomFieldDefinition.Create(_tenantId, "Field1", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<DeleteCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new DeleteCustomFieldDefinitionCommand(definition.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.CustomFieldDefinitions.FindAsync(definition.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotFound_ShouldFail()
    {
        // Arrange
        var handler = new DeleteCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<DeleteCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new DeleteCustomFieldDefinitionCommand(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_definition_not_found");
    }

    [Fact]
    public async Task Handle_AlreadyDeactivated_ShouldFail()
    {
        // Arrange
        var definition = CustomFieldDefinition.Create(_tenantId, "Field1", "text");
        definition.Deactivate();
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<DeleteCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new DeleteCustomFieldDefinitionCommand(definition.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_definition_already_deactivated");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
