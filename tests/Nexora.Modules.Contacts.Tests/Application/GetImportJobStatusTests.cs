using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetImportJobStatusTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetImportJobStatusTests()
    {
        ITenantContextAccessor tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, tenantAccessor);
    }

    [Fact]
    public async Task Handle_NonExistentJob_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new GetImportJobStatusHandler(
            _dbContext, NullLogger<GetImportJobStatusHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetImportJobStatusQuery(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_job_not_found");
    }

    [Fact]
    public async Task Handle_ExistingJob_ShouldReturnStatus()
    {
        // Arrange
        var importJob = ImportJob.Create(
            _tenantId, _orgId, "test.csv", "csv", $"{_orgId}/contacts/imports/abc/test.csv", "user-1");
        await _dbContext.ImportJobs.AddAsync(importJob);
        await _dbContext.SaveChangesAsync();

        var handler = new GetImportJobStatusHandler(
            _dbContext, NullLogger<GetImportJobStatusHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetImportJobStatusQuery(importJob.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.JobId.Should().Be(importJob.Id.Value);
        result.Value.Status.Should().Be("Queued");
        result.Value.TotalRows.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CompletedJob_ShouldReturnCompletedStatus()
    {
        // Arrange
        var importJob = ImportJob.Create(
            _tenantId, _orgId, "test.csv", "csv", $"{_orgId}/contacts/imports/abc/test.csv", "user-1");
        importJob.MarkProcessing(100);
        importJob.UpdateProgress(100, 95, 5);
        importJob.MarkCompleted();
        await _dbContext.ImportJobs.AddAsync(importJob);
        await _dbContext.SaveChangesAsync();

        var handler = new GetImportJobStatusHandler(
            _dbContext, NullLogger<GetImportJobStatusHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetImportJobStatusQuery(importJob.Id.Value),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Completed");
        result.Value.TotalRows.Should().Be(100);
        result.Value.SuccessCount.Should().Be(95);
        result.Value.ErrorCount.Should().Be(5);
        result.Value.CompletedAt.Should().NotBeNull();
    }

    public void Dispose() => _dbContext.Dispose();
}
