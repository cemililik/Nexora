using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateCustomFieldDefinitionTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateCustomFieldDefinitionTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ShouldUpdate()
    {
        // Arrange
        var definition = CustomFieldDefinition.Create(_tenantId, "OldName", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateCustomFieldDefinitionCommand(definition.Id.Value, "NewName", null, true, 5),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FieldName.Should().Be("NewName");
        result.Value.IsRequired.Should().BeTrue();
        result.Value.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldFail()
    {
        // Arrange
        var handler = new UpdateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateCustomFieldDefinitionCommand(Guid.NewGuid(), "Name", null, false, 0),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_definition_not_found");
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldFail()
    {
        // Arrange
        var def1 = CustomFieldDefinition.Create(_tenantId, "Field1", "text");
        var def2 = CustomFieldDefinition.Create(_tenantId, "Field2", "number");
        await _dbContext.CustomFieldDefinitions.AddRangeAsync(def1, def2);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateCustomFieldDefinitionCommand(def2.Id.Value, "Field1", null, false, 0),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_name_duplicate");
    }

    [Fact]
    public async Task Handle_SameNameSameDefinition_ShouldSucceed()
    {
        // Arrange
        var definition = CustomFieldDefinition.Create(_tenantId, "Field1", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<UpdateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new UpdateCustomFieldDefinitionCommand(definition.Id.Value, "Field1", "new options", true, 10),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
