using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class SetContactCustomFieldTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public SetContactCustomFieldTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NewField_ShouldCreate()
    {
        // Arrange
        var (contact, definition) = await SeedContactAndDefinition();
        var handler = new SetContactCustomFieldHandler(_dbContext, _tenantAccessor, NullLogger<SetContactCustomFieldHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new SetContactCustomFieldCommand(contact.Id.Value, definition.Id.Value, "MyValue"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("MyValue");
        result.Value.FieldName.Should().Be("Nickname");
    }

    [Fact]
    public async Task Handle_ExistingField_ShouldUpdate()
    {
        // Arrange
        var (contact, definition) = await SeedContactAndDefinition();
        var existing = ContactCustomField.Create(contact.Id, definition.Id, "OldValue");
        await _dbContext.ContactCustomFields.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new SetContactCustomFieldHandler(_dbContext, _tenantAccessor, NullLogger<SetContactCustomFieldHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new SetContactCustomFieldCommand(contact.Id.Value, definition.Id.Value, "NewValue"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("NewValue");
        var count = await _dbContext.ContactCustomFields.CountAsync(f => f.ContactId == contact.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RequiredFieldEmptyValue_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        var definition = CustomFieldDefinition.Create(_tenantId, "Required Field", "text", null, true);
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new SetContactCustomFieldHandler(_dbContext, _tenantAccessor, NullLogger<SetContactCustomFieldHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new SetContactCustomFieldCommand(contact.Id.Value, definition.Id.Value, null),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_value_required");
    }

    [Fact]
    public async Task Handle_ContactNotFound_ShouldFail()
    {
        // Arrange
        var handler = new SetContactCustomFieldHandler(_dbContext, _tenantAccessor, NullLogger<SetContactCustomFieldHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new SetContactCustomFieldCommand(Guid.NewGuid(), Guid.NewGuid(), "Value"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_contact_not_found");
    }

    [Fact]
    public async Task Handle_DefinitionNotFound_ShouldFail()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var handler = new SetContactCustomFieldHandler(_dbContext, _tenantAccessor, NullLogger<SetContactCustomFieldHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new SetContactCustomFieldCommand(contact.Id.Value, Guid.NewGuid(), "Value"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_custom_field_definition_not_found");
    }

    private async Task<(Contact contact, CustomFieldDefinition definition)> SeedContactAndDefinition()
    {
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, null, null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        var definition = CustomFieldDefinition.Create(_tenantId, "Nickname", "text");
        await _dbContext.CustomFieldDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();
        return (contact, definition);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
