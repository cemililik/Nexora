using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class ActivateDeactivateTemplateTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ActivateDeactivateTemplateTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<Guid> SeedTemplateAsync(bool active = true)
    {
        var template = DocumentTemplate.Create(_tenantId, _orgId, "Template", TemplateCategory.Contract, TemplateFormat.Pdf, "key");
        if (!active) template.Deactivate();
        await _dbContext.DocumentTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template.Id.Value;
    }

    [Fact]
    public async Task Deactivate_ActiveTemplate_DeactivatesTemplate()
    {
        var templateId = await SeedTemplateAsync();
        var handler = new DeactivateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<DeactivateDocumentTemplateHandler>.Instance);

        var result = await handler.Handle(new DeactivateDocumentTemplateCommand(templateId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.DocumentTemplates.FindAsync(DocumentTemplateId.From(templateId));
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Activate_InactiveTemplate_ActivatesTemplate()
    {
        var templateId = await SeedTemplateAsync(active: false);
        var handler = new ActivateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<ActivateDocumentTemplateHandler>.Instance);

        var result = await handler.Handle(new ActivateDocumentTemplateCommand(templateId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.DocumentTemplates.FindAsync(DocumentTemplateId.From(templateId));
        updated!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_NonExistentTemplate_ReturnsFailure()
    {
        var handler = new DeactivateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<DeactivateDocumentTemplateHandler>.Instance);

        var result = await handler.Handle(new DeactivateDocumentTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Activate_NonExistentTemplate_ReturnsFailure()
    {
        var handler = new ActivateDocumentTemplateHandler(_dbContext, _tenantAccessor, NullLogger<ActivateDocumentTemplateHandler>.Instance);

        var result = await handler.Handle(new ActivateDocumentTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
