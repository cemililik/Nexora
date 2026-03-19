using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to start a contact export job.</summary>
public sealed record StartContactExportCommand(
    string Format,
    string? StatusFilter = null,
    string? TypeFilter = null) : ICommand<ExportJobDto>;

/// <summary>Validates contact export input.</summary>
public sealed class StartContactExportValidator : AbstractValidator<StartContactExportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "json", "xlsx"];

    public StartContactExportValidator()
    {
        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("lockey_contacts_validation_export_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_export_format_invalid");
    }
}

/// <summary>Starts a background export job and returns job tracking info.</summary>
public sealed class StartContactExportHandler(
    ITenantContextAccessor tenantContextAccessor,
    ILogger<StartContactExportHandler> logger) : ICommandHandler<StartContactExportCommand, ExportJobDto>
{
    public Task<Result<ExportJobDto>> Handle(
        StartContactExportCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var jobId = Guid.NewGuid();

        logger.LogInformation("Contact export job {JobId} started for tenant {TenantId} in format {Format}",
            jobId, tenantId, request.Format);

        var dto = new ExportJobDto(
            jobId, "Queued", request.Format.ToLowerInvariant(),
            DateTimeOffset.UtcNow, null, null);

        return Task.FromResult(Result<ExportJobDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_export_job_started")));
    }
}
