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
    private readonly Guid _userId = Guid.NewGuid();

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
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        await _dbContext.SaveChangesAsync();
        return folder.Id;
    }

    private Document CreateDocument(FolderId folderId, string name = "test.pdf") =>
        Document.Create(_tenantId, _orgId, folderId, _userId, name, "application/pdf", 1024, $"storage/{name}");

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

    [Fact]
    public async Task Handle_WithStatusFilter_ShouldReturnOnlyMatchingStatus()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var activeDoc = CreateDocument(folderId, "active.pdf");
        var archivedDoc = CreateDocument(folderId, "archived.pdf");
        archivedDoc.Archive();
        await _dbContext.Documents.AddRangeAsync(activeDoc, archivedDoc);
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(Status: "Archived"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("archived.pdf");
    }

    [Fact]
    public async Task Handle_WithLinkedEntityIdFilter_ShouldReturnOnlyLinkedDocument()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var entityId = Guid.NewGuid();
        var linkedDoc = CreateDocument(folderId, "linked.pdf");
        linkedDoc.LinkToEntity(entityId, "Contact");
        var unlinkedDoc = CreateDocument(folderId, "unlinked.pdf");
        await _dbContext.Documents.AddRangeAsync(linkedDoc, unlinkedDoc);
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(LinkedEntityId: entityId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("linked.pdf");
    }

    [Fact]
    public async Task Handle_WithLinkedEntityTypeFilter_ShouldReturnOnlyMatchingType()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var contactDoc = CreateDocument(folderId, "contact.pdf");
        contactDoc.LinkToEntity(Guid.NewGuid(), "Contact");
        var orderDoc = CreateDocument(folderId, "order.pdf");
        orderDoc.LinkToEntity(Guid.NewGuid(), "Order");
        await _dbContext.Documents.AddRangeAsync(contactDoc, orderDoc);
        await _dbContext.SaveChangesAsync();
        var handler = new GetDocumentsHandler(_dbContext, _tenantAccessor, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(LinkedEntityType: "Contact"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("contact.pdf");
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
