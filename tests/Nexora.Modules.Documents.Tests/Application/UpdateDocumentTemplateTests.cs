using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class UpdateDocumentTemplateTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateDocumentTemplateTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<Guid> SeedTemplateAsync()
    {
        var template = DocumentTemplate.Create(_tenantId, _orgId, "Original", TemplateCategory.Contract, TemplateFormat.Pdf, "key");
        await _dbContext.DocumentTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template.Id.Value;
    }

    [Fact]
    public async Task Handle_ValidUpdate_UpdatesTemplate()
    {
        var templateId = await SeedTemplateAsync();
        var handler = new UpdateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<UpdateDocumentTemplateHandler>.Instance);
        var command = new UpdateDocumentTemplateCommand(templateId, "Updated", "Receipt", "Html");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Updated");
        result.Value.Category.Should().Be("Receipt");
        result.Value.Format.Should().Be("Html");
    }

    [Fact]
    public async Task Handle_WithVariableDefinitions_UpdatesVariables()
    {
        var templateId = await SeedTemplateAsync();
        var handler = new UpdateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<UpdateDocumentTemplateHandler>.Instance);
        var varDefs = """{"amount": {"required": true}}""";
        var command = new UpdateDocumentTemplateCommand(templateId, "Updated", "Contract", "Pdf", varDefs);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.VariableDefinitions.Should().Be(varDefs);
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ReturnsFailure()
    {
        var handler = new UpdateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<UpdateDocumentTemplateHandler>.Instance);
        var command = new UpdateDocumentTemplateCommand(Guid.NewGuid(), "Name", "Contract", "Pdf");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidUpdate_PersistsChanges()
    {
        var templateId = await SeedTemplateAsync();
        var handler = new UpdateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<UpdateDocumentTemplateHandler>.Instance);
        var command = new UpdateDocumentTemplateCommand(templateId, "Persisted", "Letter", "Docx");

        await handler.Handle(command, CancellationToken.None);

        var updated = await _dbContext.DocumentTemplates.FindAsync(DocumentTemplateId.From(templateId));
        updated!.Name.Should().Be("Persisted");
        updated.Category.Should().Be(TemplateCategory.Letter);
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
