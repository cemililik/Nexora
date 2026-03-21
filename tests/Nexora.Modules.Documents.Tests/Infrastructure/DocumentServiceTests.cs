using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using MediatR;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class DocumentServiceTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly DocumentService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DocumentServiceTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, accessor);
        _mediator = Substitute.For<IMediator>();
        _service = new DocumentService(_dbContext, _mediator, NullLogger<DocumentService>.Instance);
    }

    [Fact]
    public async Task GenerateFromTemplateAsync_Success_ReturnsResult()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var request = new GenerateFromTemplateRequest(templateId, folderId, "output.pdf", new Dictionary<string, string> { ["name"] = "Test" });

        _mediator.Send(Arg.Any<RenderDocumentTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RenderTemplateResultDto>.Success(
                new RenderTemplateResultDto(docId, "output.pdf", "key/output.pdf"),
                LocalizedMessage.Of("lockey_documents_template_rendered")));

        // Act
        var result = await _service.GenerateFromTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.DocumentId.Should().Be(docId);
        result.Name.Should().Be("output.pdf");
        result.StorageKey.Should().Be("key/output.pdf");
    }

    [Fact]
    public async Task GenerateFromTemplateAsync_Failure_ReturnsNull()
    {
        // Arrange
        var request = new GenerateFromTemplateRequest(Guid.NewGuid(), Guid.NewGuid(), "output.pdf", []);

        _mediator.Send(Arg.Any<RenderDocumentTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_template_not_found")));

        // Act
        var result = await _service.GenerateFromTemplateAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateFromTemplateAsync_ValidRequest_PassesCorrectCommand()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var variables = new Dictionary<string, string> { ["key1"] = "val1", ["key2"] = "val2" };
        var request = new GenerateFromTemplateRequest(templateId, folderId, "report.pdf", variables);

        _mediator.Send(Arg.Any<RenderDocumentTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RenderTemplateResultDto>.Success(
                new RenderTemplateResultDto(Guid.NewGuid(), "report.pdf", "key/report.pdf"),
                LocalizedMessage.Of("lockey_documents_template_rendered")));

        // Act
        await _service.GenerateFromTemplateAsync(request);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<RenderDocumentTemplateCommand>(c =>
                c.TemplateId == templateId &&
                c.FolderId == folderId &&
                c.OutputName == "report.pdf" &&
                c.Variables.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDocumentsByEntityAsync_LinkedDocuments_ReturnsDocuments()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);
        await _dbContext.Folders.AddAsync(folder);

        var linkedDoc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "linked.pdf", "application/pdf", 1024, "key/linked.pdf");
        linkedDoc.LinkToEntity(entityId, "Contact");

        var unlinkedDoc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "unlinked.pdf", "application/pdf", 512, "key/unlinked.pdf");

        await _dbContext.Documents.AddRangeAsync(linkedDoc, unlinkedDoc);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDocumentsByEntityAsync(entityId, "Contact");

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("linked.pdf");
        result[0].LinkedEntityId.Should().Be(entityId);
        result[0].LinkedEntityType.Should().Be("Contact");
    }

    [Fact]
    public async Task GetDocumentsByEntityAsync_NoMatches_ReturnsEmpty()
    {
        // Act
        var result = await _service.GetDocumentsByEntityAsync(Guid.NewGuid(), "Contact");

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
