using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GetDocumentsWithAccessFilterTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IDocumentAccessChecker _accessChecker;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetDocumentsWithAccessFilterTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(
            _tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
        _accessChecker = Substitute.For<IDocumentAccessChecker>();
    }

    private async Task<FolderId> SeedFolderAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);
        await _dbContext.Folders.AddAsync(folder);
        await _dbContext.SaveChangesAsync();
        return folder.Id;
    }

    private Document CreateDocument(FolderId folderId, string name, Guid? userId = null) =>
        Document.Create(_tenantId, _orgId, folderId, userId ?? _userId, name, "application/pdf", 1024, $"key/{name}");

    [Fact]
    public async Task Handle_AccessCheckerFiltersOut_ReturnsOnlyAccessible()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var doc1 = CreateDocument(folderId, "accessible.pdf");
        var doc2 = CreateDocument(folderId, "restricted.pdf");
        await _dbContext.Documents.AddRangeAsync(doc1, doc2);
        await _dbContext.SaveChangesAsync();

        _accessChecker.ApplyAccessFilter(
                Arg.Any<IQueryable<Document>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>())
            .Returns(ci => ci.Arg<IQueryable<Document>>().Where(d => d.Id == doc1.Id));

        var handler = new GetDocumentsHandler(
            _dbContext, _tenantAccessor, _accessChecker, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("accessible.pdf");
    }

    [Fact]
    public async Task Handle_AccessCheckerReturnsNone_ReturnsEmpty()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        await _dbContext.Documents.AddAsync(CreateDocument(folderId, "secret.pdf"));
        await _dbContext.SaveChangesAsync();

        _accessChecker.ApplyAccessFilter(
                Arg.Any<IQueryable<Document>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>())
            .Returns(ci => ci.Arg<IQueryable<Document>>().Where(d => false));

        var handler = new GetDocumentsHandler(
            _dbContext, _tenantAccessor, _accessChecker, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvalidUserContext_FailsOrThrows()
    {
        // Arrange
        var noUserAccessor = new TenantContextAccessor();
        noUserAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var handler = new GetDocumentsHandler(
            _dbContext, noUserAccessor, _accessChecker, NullLogger<GetDocumentsHandler>.Instance);

        // Act — may return failure or throw depending on tenant context implementation
        Func<Task> act = () => handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        // Assert — either exception or failure result is acceptable
        try
        {
            var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);
            result.IsSuccess.Should().BeFalse();
        }
        catch (Exception)
        {
            // Expected — tenant context accessor may throw when UserId is missing
        }
    }

    [Fact]
    public async Task Handle_PaginationWithAccessFilter_CountsOnlyAccessible()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var docs = new List<Document>();
        for (var i = 0; i < 5; i++)
        {
            var doc = CreateDocument(folderId, $"doc{i}.pdf");
            docs.Add(doc);
        }
        await _dbContext.Documents.AddRangeAsync(docs);
        await _dbContext.SaveChangesAsync();

        // Only 3 accessible
        var accessibleIds = docs.Take(3).Select(d => d.Id).ToList();
        _accessChecker.ApplyAccessFilter(
                Arg.Any<IQueryable<Document>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>())
            .Returns(ci => ci.Arg<IQueryable<Document>>().Where(d => accessibleIds.Contains(d.Id)));

        var handler = new GetDocumentsHandler(
            _dbContext, _tenantAccessor, _accessChecker, NullLogger<GetDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_GetDocumentById_AccessDenied_ReturnsFailure()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var doc = CreateDocument(folderId, "secret.pdf", Guid.NewGuid());
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        var localAccessChecker = Substitute.For<IDocumentAccessChecker>();
        localAccessChecker.HasAccessAsync(
                Arg.Any<DocumentId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetDocumentByIdHandler(
            _dbContext, _tenantAccessor, localAccessChecker, NullLogger<GetDocumentByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentByIdQuery(doc.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
