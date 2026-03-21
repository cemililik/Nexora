using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class RenderDocumentTemplateTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public RenderDocumentTemplateTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<(Guid TemplateId, Guid FolderId)> SeedTemplateAndFolderAsync(
        string? variableDefinitions = null, bool active = true)
    {
        var folder = Folder.Create(_tenantId, _orgId, "Output", _userId);
        await _dbContext.Folders.AddAsync(folder);

        var template = DocumentTemplate.Create(
            _tenantId, _orgId, "Contract Template", TemplateCategory.Contract, TemplateFormat.Pdf,
            "templates/contract.pdf", variableDefinitions);
        if (!active) template.Deactivate();
        await _dbContext.DocumentTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();

        return (template.Id.Value, folder.Id.Value);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesDocument()
    {
        var (templateId, folderId) = await SeedTemplateAndFolderAsync();
        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(templateId, folderId, "output.pdf", new());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("output.pdf");
        result.Value.StorageKey.Should().Contain("output.pdf");
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsDocument()
    {
        var (templateId, folderId) = await SeedTemplateAndFolderAsync();
        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(templateId, folderId, "output.pdf", new());

        await handler.Handle(command, CancellationToken.None);

        var count = await _dbContext.Documents.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_InactiveTemplate_ReturnsFailure()
    {
        var (templateId, folderId) = await SeedTemplateAndFolderAsync(active: false);
        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(templateId, folderId, "output.pdf", new());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ReturnsFailure()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(Guid.NewGuid(), folder.Id.Value, "output.pdf", new());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentFolder_ReturnsFailure()
    {
        var (templateId, _) = await SeedTemplateAndFolderAsync();
        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(templateId, Guid.NewGuid(), "output.pdf", new());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FolderInDifferentOrganization_ReturnsFailure()
    {
        // Arrange — template in our org, folder in a different org
        var (templateId, _) = await SeedTemplateAndFolderAsync();
        var otherOrgId = Guid.NewGuid();
        var otherFolder = Folder.Create(_tenantId, otherOrgId, "OtherOrgFolder", _userId);
        await _dbContext.Folders.AddAsync(otherFolder);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(templateId, otherFolder.Id.Value, "output.pdf", new());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MissingRequiredVariable_ReturnsFailure()
    {
        var varDefs = """{"companyName": {"required": true}}""";
        var (templateId, folderId) = await SeedTemplateAndFolderAsync(variableDefinitions: varDefs);
        var handler = CreateHandler();
        var command = new RenderDocumentTemplateCommand(templateId, folderId, "output.pdf", new());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithRequiredVariable_Succeeds()
    {
        var varDefs = """{"companyName": {"required": true}}""";
        var (templateId, folderId) = await SeedTemplateAndFolderAsync(variableDefinitions: varDefs);
        var handler = CreateHandler();
        var variables = new Dictionary<string, string> { { "companyName", "Acme Corp" } };
        var command = new RenderDocumentTemplateCommand(templateId, folderId, "output.pdf", variables);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();

    private RenderDocumentTemplateHandler CreateHandler() =>
        new(_dbContext, _tenantAccessor, NullLogger<RenderDocumentTemplateHandler>.Instance);

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
