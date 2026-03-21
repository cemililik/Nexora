using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GetDocumentTemplatesTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetDocumentTemplatesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmpty()
    {
        var handler = new GetDocumentTemplatesHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplatesHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithTemplates_ReturnsAll()
    {
        await _dbContext.DocumentTemplates.AddRangeAsync(
            DocumentTemplate.Create(_tenantId, _orgId, "T1", TemplateCategory.Contract, TemplateFormat.Pdf, "k1"),
            DocumentTemplate.Create(_tenantId, _orgId, "T2", TemplateCategory.Receipt, TemplateFormat.Html, "k2"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetDocumentTemplatesHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplatesHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_FiltersByCategory()
    {
        await _dbContext.DocumentTemplates.AddRangeAsync(
            DocumentTemplate.Create(_tenantId, _orgId, "Contract", TemplateCategory.Contract, TemplateFormat.Pdf, "k1"),
            DocumentTemplate.Create(_tenantId, _orgId, "Receipt", TemplateCategory.Receipt, TemplateFormat.Pdf, "k2"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetDocumentTemplatesHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplatesHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplatesQuery(Category: "Contract"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("Contract");
    }

    [Fact]
    public async Task Handle_WithActiveFilter_FiltersByActiveStatus()
    {
        var active = DocumentTemplate.Create(_tenantId, _orgId, "Active", TemplateCategory.Letter, TemplateFormat.Pdf, "k1");
        var inactive = DocumentTemplate.Create(_tenantId, _orgId, "Inactive", TemplateCategory.Letter, TemplateFormat.Pdf, "k2");
        inactive.Deactivate();
        await _dbContext.DocumentTemplates.AddRangeAsync(active, inactive);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDocumentTemplatesHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplatesHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplatesQuery(IsActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("Active");
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
