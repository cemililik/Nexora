using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class ReportScheduleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ReportDefinitionId _definitionId = ReportDefinitionId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 8 * * 1",
            ReportFormat.Excel, "[\"admin@test.com\"]");

        schedule.Id.Value.Should().NotBeEmpty();
        schedule.TenantId.Should().Be(_tenantId);
        schedule.DefinitionId.Should().Be(_definitionId);
        schedule.CronExpression.Should().Be("0 8 * * 1");
        schedule.Format.Should().Be(ReportFormat.Excel);
        schedule.Recipients.Should().Be("[\"admin@test.com\"]");
        schedule.IsActive.Should().BeTrue();
        schedule.LastExecutionAt.Should().BeNull();
        schedule.NextExecutionAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);

        schedule.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReportScheduleCreatedEvent>();
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);

        schedule.Update("0 8 * * 1", ReportFormat.Pdf, "[\"test@test.com\"]");

        schedule.CronExpression.Should().Be("0 8 * * 1");
        schedule.Format.Should().Be(ReportFormat.Pdf);
        schedule.Recipients.Should().Be("[\"test@test.com\"]");
    }

    [Fact]
    public void RecordExecution_ShouldUpdateTimestamps()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);
        var now = DateTimeOffset.UtcNow;
        var next = now.AddDays(1);

        schedule.RecordExecution(now, next);

        schedule.LastExecutionAt.Should().Be(now);
        schedule.NextExecutionAt.Should().Be(next);
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);

        schedule.Deactivate();

        schedule.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);
        schedule.Deactivate();

        var act = () => schedule.Deactivate();

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_schedule_already_inactive");
    }

    [Fact]
    public void Activate_WhenInactive_ShouldSetActive()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);
        schedule.Deactivate();

        schedule.Activate();

        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldThrow()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *",
            ReportFormat.Csv, null);

        var act = () => schedule.Activate();

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_schedule_already_active");
    }
}
