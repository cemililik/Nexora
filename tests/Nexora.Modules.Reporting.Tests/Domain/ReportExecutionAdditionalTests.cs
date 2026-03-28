using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class ReportExecutionAdditionalTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ReportDefinitionId _definitionId = ReportDefinitionId.New();

    [Fact]
    public void Create_WithParameterValues_ShouldSetParameterValues()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv,
            "{\"startDate\":\"2026-01-01\"}", "user@test.com");

        execution.ParameterValues.Should().Be("{\"startDate\":\"2026-01-01\"}");
    }

    [Fact]
    public void Create_WithNullExecutedBy_ShouldSetToNull()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);

        execution.ExecutedBy.Should().BeNull();
    }

    [Fact]
    public void Create_WhenCalled_GeneratesUniqueIds()
    {
        var exec1 = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        var exec2 = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);

        exec1.Id.Value.Should().NotBe(exec2.Id.Value);
    }

    [Fact]
    public void MarkRunning_WithJobId_ShouldSetHangfireJobId()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);

        execution.MarkRunning("hangfire-job-42");

        execution.HangfireJobId.Should().Be("hangfire-job-42");
    }

    [Fact]
    public void MarkRunning_WithoutJobId_ShouldNotThrow()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);

        var act = () => execution.MarkRunning();

        act.Should().NotThrow();
        execution.Status.Should().Be(ReportStatus.Running);
    }

    [Fact]
    public void FullLifecycle_QueuedToRunningToCompleted_ShouldTransitionCorrectly()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Excel, null, "admin@test.com");

        execution.Status.Should().Be(ReportStatus.Queued);

        execution.MarkRunning("job-1");
        execution.Status.Should().Be(ReportStatus.Running);

        execution.MarkCompleted("reports/output.xlsx", 100, 2500);
        execution.Status.Should().Be(ReportStatus.Completed);
        execution.RowCount.Should().Be(100);
        execution.DurationMs.Should().Be(2500);
        execution.ResultStorageKey.Should().Be("reports/output.xlsx");
    }

    [Fact]
    public void FullLifecycle_QueuedToRunningToFailed_ShouldTransitionCorrectly()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Pdf, null, null);

        execution.MarkRunning();
        execution.MarkFailed("Connection timeout", 5000);

        execution.Status.Should().Be(ReportStatus.Failed);
        execution.ErrorDetails.Should().Be("Connection timeout");
        execution.DurationMs.Should().Be(5000);
    }

    [Fact]
    public void MarkCompleted_ShouldNotRaiseDomainEventTwice()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        execution.MarkRunning();
        execution.MarkCompleted("key", 10, 100);

        execution.DomainEvents.OfType<ReportExecutionCompletedEvent>()
            .Should().ContainSingle();
    }
}
