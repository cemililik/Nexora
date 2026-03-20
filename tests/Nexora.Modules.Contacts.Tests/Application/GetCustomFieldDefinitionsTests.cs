using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetCustomFieldDefinitionsTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetCustomFieldDefinitionsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithDefinitions_ShouldReturn()
    {
        // Arrange
        var def1 = CustomFieldDefinition.Create(_tenantId, "Field1", "text", null, false, 1);
        var def2 = CustomFieldDefinition.Create(_tenantId, "Field2", "number", null, false, 0);
        await _dbContext.CustomFieldDefinitions.AddRangeAsync(def1, def2);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetCustomFieldDefinitionsHandler(_dbContext, _tenantAccessor, NullLogger<GetCustomFieldDefinitionsHandler>.Instance);
        var result = await handler.Handle(new GetCustomFieldDefinitionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value[0].FieldName.Should().Be("Field2"); // DisplayOrder 0 first
    }

    [Fact]
    public async Task Handle_FilterActive_ShouldFilterCorrectly()
    {
        // Arrange
        var active = CustomFieldDefinition.Create(_tenantId, "Active", "text");
        var inactive = CustomFieldDefinition.Create(_tenantId, "Inactive", "text");
        inactive.Deactivate();
        await _dbContext.CustomFieldDefinitions.AddRangeAsync(active, inactive);
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetCustomFieldDefinitionsHandler(_dbContext, _tenantAccessor, NullLogger<GetCustomFieldDefinitionsHandler>.Instance);
        var result = await handler.Handle(new GetCustomFieldDefinitionsQuery(IsActive: true), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].FieldName.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_NoDefinitions_ShouldReturnEmpty()
    {
        // Arrange
        var handler = new GetCustomFieldDefinitionsHandler(_dbContext, _tenantAccessor, NullLogger<GetCustomFieldDefinitionsHandler>.Instance);
        var result = await handler.Handle(new GetCustomFieldDefinitionsQuery(), CancellationToken.None);

        // Act & Assert
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
