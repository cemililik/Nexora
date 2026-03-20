using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class DocumentQueryTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DocumentQueryTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<FolderId> SeedFolderAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _orgId);
        await _dbContext.Folders.AddAsync(folder);
        await _dbContext.SaveChangesAsync();
        return folder.Id;
    }

    private Document CreateDocument(FolderId folderId, string name = "test.pdf") =>
        Document.Create(_tenantId, _orgId, folderId, _orgId, name, "application/pdf", 1024, $"storage/{name}");

    [Fact]
    public async Task Handle_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithDocuments_ShouldReturnAll()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        await _dbContext.Documents.AddRangeAsync(
            CreateDocument(folderId, "a.pdf"),
            CreateDocument(folderId, "b.pdf"));
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithSearchFilter_ShouldFilterByName()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        await _dbContext.Documents.AddRangeAsync(
            CreateDocument(folderId, "report.pdf"),
            CreateDocument(folderId, "invoice.pdf"));
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(Search: "report"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("report.pdf");
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        for (var i = 0; i < 5; i++)
            await _dbContext.Documents.AddAsync(CreateDocument(folderId, $"doc{i}.pdf"));
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldNotReturnOtherTenantDocs()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var otherTenantDoc = Document.Create(Guid.NewGuid(), Guid.NewGuid(), folderId, Guid.NewGuid(),
            "other.pdf", "application/pdf", 100, "key");
        await _dbContext.Documents.AddAsync(otherTenantDoc);
        await _dbContext.Documents.AddAsync(CreateDocument(folderId, "mine.pdf"));
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("mine.pdf");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
