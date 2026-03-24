using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class ReportExecutionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ReportDefinitionId _definitionId = ReportDefinitionId.New();

    [Fact]
    public void Create_ShouldSetQueuedStatus()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, "user@test.com");

        execution.Id.Value.Should().NotBeEmpty();
        execution.TenantId.Should().Be(_tenantId);
        execution.DefinitionId.Should().Be(_definitionId);
        execution.Status.Should().Be(ReportStatus.Queued);
        execution.Format.Should().Be(ReportFormat.Csv);
        execution.ExecutedBy.Should().Be("user@test.com");
        execution.RowCount.Should().BeNull();
        execution.DurationMs.Should().BeNull();
    }

    [Fact]
    public void MarkRunning_ShouldTransitionFromQueued()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Excel, null, null);

        execution.MarkRunning("job-123");

        execution.Status.Should().Be(ReportStatus.Running);
        execution.HangfireJobId.Should().Be("job-123");
    }

    [Fact]
    public void MarkRunning_WhenNotQueued_ShouldThrow()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        execution.MarkRunning();
        execution.MarkCompleted("key", 10, 500);

        var act = () => execution.MarkRunning();

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_execution_not_queued");
    }

    [Fact]
    public void MarkCompleted_ShouldSetResultDetails()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Pdf, null, null);
        execution.MarkRunning();

        execution.MarkCompleted("reports/test.pdf", 42, 1500);

        execution.Status.Should().Be(ReportStatus.Completed);
        execution.ResultStorageKey.Should().Be("reports/test.pdf");
        execution.RowCount.Should().Be(42);
        execution.DurationMs.Should().Be(1500);
    }

    [Fact]
    public void MarkCompleted_ShouldRaiseDomainEvent()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        execution.MarkRunning();

        execution.MarkCompleted("key", 10, 100);

        execution.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReportExecutionCompletedEvent>();
    }

    [Fact]
    public void MarkCompleted_WhenNotRunning_ShouldThrow()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);

        var act = () => execution.MarkCompleted("key", 10, 100);

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_execution_not_running");
    }

    [Fact]
    public void MarkFailed_ShouldSetErrorDetails()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        execution.MarkRunning();

        execution.MarkFailed("Timeout exceeded", 30000);

        execution.Status.Should().Be(ReportStatus.Failed);
        execution.ErrorDetails.Should().Be("Timeout exceeded");
        execution.DurationMs.Should().Be(30000);
    }

    [Fact]
    public void MarkFailed_FromQueued_ShouldSetErrorDetails()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);

        execution.MarkFailed("Definition not found", 0);

        execution.Status.Should().Be(ReportStatus.Failed);
        execution.ErrorDetails.Should().Be("Definition not found");
        execution.DurationMs.Should().Be(0);
    }

    [Fact]
    public void MarkFailed_WhenAlreadyCompleted_ShouldThrow()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        execution.MarkRunning();
        execution.MarkCompleted("key", 10, 500);

        var act = () => execution.MarkFailed("Error", 100);

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_execution_already_finished");
    }

    [Fact]
    public void MarkFailed_WhenAlreadyFailed_ShouldThrow()
    {
        var execution = ReportExecution.Create(
            _tenantId, _definitionId, ReportFormat.Csv, null, null);
        execution.MarkRunning();
        execution.MarkFailed("First error", 100);

        var act = () => execution.MarkFailed("Second error", 200);

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_execution_already_finished");
    }
}
