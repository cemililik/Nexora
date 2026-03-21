using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class CreateDocumentTemplateTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateDocumentTemplateTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesTemplate()
    {
        var handler = new CreateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<CreateDocumentTemplateHandler>.Instance);
        var command = new CreateDocumentTemplateCommand("Invoice Template", "Receipt", "Pdf", "templates/invoice.pdf");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Invoice Template");
        result.Value.Category.Should().Be("Receipt");
        result.Value.Format.Should().Be("Pdf");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithVariableDefinitions_SetsVariables()
    {
        var handler = new CreateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<CreateDocumentTemplateHandler>.Instance);
        var varDefs = """{"name": {"required": true}, "date": {"required": false}}""";
        var command = new CreateDocumentTemplateCommand("Contract", "Contract", "Docx", "templates/contract.docx", varDefs);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.VariableDefinitions.Should().Be(varDefs);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsToDatabase()
    {
        var handler = new CreateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<CreateDocumentTemplateHandler>.Instance);
        var command = new CreateDocumentTemplateCommand("Template", "Letter", "Html", "templates/letter.html");

        await handler.Handle(command, CancellationToken.None);

        var count = await _dbContext.DocumentTemplates.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NameWithWhitespace_TrimsName()
    {
        var handler = new CreateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<CreateDocumentTemplateHandler>.Instance);
        var command = new CreateDocumentTemplateCommand("  Trimmed  ", "Report", "Pdf", "templates/report.pdf");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Value!.Name.Should().Be("Trimmed");
    }

    [Fact]
    public async Task Handle_AllCategories_CreatesSuccessfully()
    {
        var handler = new CreateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<CreateDocumentTemplateHandler>.Instance);

        foreach (var category in new[] { "Contract", "Receipt", "Letter", "Report" })
        {
            var command = new CreateDocumentTemplateCommand($"Template {category}", category, "Pdf", $"key/{category}");
            var result = await handler.Handle(command, CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        }
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
