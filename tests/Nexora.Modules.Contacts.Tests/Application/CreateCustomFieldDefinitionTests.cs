using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class CreateCustomFieldDefinitionTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateCustomFieldDefinitionTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidDefinition_ShouldCreate()
    {
        // Arrange
        var handler = new CreateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<CreateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new CreateCustomFieldDefinitionCommand("Nickname", "text"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FieldName.Should().Be("Nickname");
        result.Value.FieldType.Should().Be("text");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithOptions_ShouldCreate()
    {
        // Arrange
        var handler = new CreateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<CreateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new CreateCustomFieldDefinitionCommand("Priority", "select", "Low,Medium,High", true, 1),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Options.Should().Be("Low,Medium,High");
        result.Value.IsRequired.Should().BeTrue();
        result.Value.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldFail()
    {
        // Arrange
        var existing = CustomFieldDefinition.Create(_tenantId, "Nickname", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<CreateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new CreateCustomFieldDefinitionCommand("Nickname", "number"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_name_duplicate");
    }

    [Fact]
    public async Task Handle_DeactivatedSameName_ShouldSucceed()
    {
        // Arrange
        var existing = CustomFieldDefinition.Create(_tenantId, "Nickname", "text");
        existing.Deactivate();
        await _dbContext.CustomFieldDefinitions.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCustomFieldDefinitionHandler(_dbContext, _tenantAccessor, NullLogger<CreateCustomFieldDefinitionHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new CreateCustomFieldDefinitionCommand("Nickname", "text"),
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
