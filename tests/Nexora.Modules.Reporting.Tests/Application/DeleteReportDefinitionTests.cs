using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class DeleteReportDefinitionTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DeleteReportDefinitionTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingDefinition_DeletesSuccessfully()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "To Delete", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteReportDefinitionHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteReportDefinitionHandler>.Instance);

        var result = await handler.Handle(
            new DeleteReportDefinitionCommand(definition.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Verify the definition is soft-deleted
        var deleted = await _dbContext.ReportDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == definition.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentDefinition_ReturnsFailure()
    {
        var handler = new DeleteReportDefinitionHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteReportDefinitionHandler>.Instance);

        var result = await handler.Handle(
            new DeleteReportDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantDefinition_ReturnsFailure()
    {
        var otherDef = ReportDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Other", null,
            "mod", null, "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(otherDef);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteReportDefinitionHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteReportDefinitionHandler>.Instance);

        var result = await handler.Handle(
            new DeleteReportDefinitionCommand(otherDef.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    public void Dispose() => _dbContext.Dispose();
}
