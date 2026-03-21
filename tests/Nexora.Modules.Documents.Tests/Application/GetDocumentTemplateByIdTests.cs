using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GetDocumentTemplateByIdTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetDocumentTemplateByIdTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingTemplate_ReturnsDetail()
    {
        var template = DocumentTemplate.Create(_tenantId, _orgId, "Contract", TemplateCategory.Contract, TemplateFormat.Pdf, "key");
        await _dbContext.DocumentTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDocumentTemplateByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplateByIdHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplateByIdQuery(template.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Contract");
        result.Value.TemplateStorageKey.Should().Be("key");
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ReturnsFailure()
    {
        var handler = new GetDocumentTemplateByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplateByIdHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplateByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DifferentTenantTemplate_ReturnsFailure()
    {
        var otherTemplate = DocumentTemplate.Create(Guid.NewGuid(), Guid.NewGuid(), "Other", TemplateCategory.Letter, TemplateFormat.Pdf, "key");
        await _dbContext.DocumentTemplates.AddAsync(otherTemplate);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDocumentTemplateByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentTemplateByIdHandler>.Instance);

        var result = await handler.Handle(new GetDocumentTemplateByIdQuery(otherTemplate.Id.Value), CancellationToken.None);

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
