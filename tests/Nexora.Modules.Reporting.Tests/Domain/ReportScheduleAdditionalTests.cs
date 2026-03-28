using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class ReportScheduleAdditionalTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ReportDefinitionId _definitionId = ReportDefinitionId.New();

    [Fact]
    public void Create_WhenCalled_GeneratesUniqueIds()
    {
        var schedule1 = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *", ReportFormat.Csv, null);
        var schedule2 = ReportSchedule.Create(
            _tenantId, _definitionId, "0 8 * * 1", ReportFormat.Csv, null);

        schedule1.Id.Value.Should().NotBe(schedule2.Id.Value);
    }

    [Fact]
    public void Update_WhenValidValues_UpdatesCronAndFormat()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *", ReportFormat.Csv, null);
        schedule.ClearDomainEvents();

        schedule.Update("0 8 * * 1", ReportFormat.Excel, "[\"test@test.com\"]");

        schedule.CronExpression.Should().Be("0 8 * * 1");
        schedule.Format.Should().Be(ReportFormat.Excel);
    }

    [Fact]
    public void RecordExecution_MultipleExecutions_ShouldTrackLatest()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *", ReportFormat.Csv, null);
        var first = DateTimeOffset.UtcNow;
        var second = first.AddDays(1);
        var nextAfterSecond = second.AddDays(1);

        schedule.RecordExecution(first, second);
        schedule.RecordExecution(second, nextAfterSecond);

        schedule.LastExecutionAt.Should().Be(second);
        schedule.NextExecutionAt.Should().Be(nextAfterSecond);
    }

    [Fact]
    public void Activate_WhenPreviouslyDeactivated_ShouldSetStateToActive()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *", ReportFormat.Csv, null);

        schedule.Deactivate();
        schedule.IsActive.Should().BeFalse();

        schedule.Activate();
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithRecipients_ShouldSetRecipients()
    {
        var recipients = "[\"user1@test.com\",\"user2@test.com\"]";
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *", ReportFormat.Pdf, recipients);

        schedule.Recipients.Should().Be(recipients);
    }

    [Fact]
    public void Create_WithNullRecipients_ShouldSetRecipientsToNull()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, _definitionId, "0 0 * * *", ReportFormat.Csv, null);

        schedule.Recipients.Should().BeNull();
    }
}
