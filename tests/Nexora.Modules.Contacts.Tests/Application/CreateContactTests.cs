using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class CreateContactTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateContactTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);

        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidIndividual_ShouldCreateContact()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Individual", "John", "Doe", null, "john@test.com", "+1234567890", "Manual");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DisplayName.Should().Be("John Doe");
        result.Value.Email.Should().Be("john@test.com");
        result.Value.Status.Should().Be("Active");
        result.Value.Type.Should().Be("Individual");
    }

    [Fact]
    public async Task Handle_ValidOrganization_ShouldCreateContact()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Organization", null, null, "Acme Corp", "info@acme.com", null, "Api");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Acme Corp");
        result.Value.Type.Should().Be("Organization");
    }

    [Fact]
    public async Task Handle_ShouldNormalizeEmail()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Individual", "Jane", "Smith", null, "JANE@Example.COM", null, "WebForm");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Individual", "Persisted", "Contact", null, null, null, "Manual");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var count = await _dbContext.Contacts.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSetTenantAndOrg()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Individual", "Test", "User", null, null, null, "Manual");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var contact = await _dbContext.Contacts.FirstAsync();
        contact.TenantId.Should().Be(_tenantId);
        contact.OrganizationId.Should().Be(_orgId);
    }

    [Fact]
    public async Task Handle_WithTitle_ShouldSetTitle()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Individual", "Ali", "Yilmaz", null, null, null, "Manual", "Dr");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Dr");
    }

    [Fact]
    public async Task Handle_MultipleContacts_ShouldHaveDistinctIds()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);

        // Act
        var r1 = await handler.Handle(new CreateContactCommand("Individual", "A", "B", null, null, null, "Manual"), CancellationToken.None);
        var r2 = await handler.Handle(new CreateContactCommand("Individual", "C", "D", null, null, null, "Manual"), CancellationToken.None);

        // Assert
        r1.Value!.Id.Should().NotBe(r2.Value!.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnSourceAsString()
    {
        // Arrange
        var handler = new CreateContactHandler(_dbContext, _tenantAccessor, NullLogger<CreateContactHandler>.Instance);
        var command = new CreateContactCommand("Individual", "Test", "User", null, null, null, "Import");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value!.Source.Should().Be("Import");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
