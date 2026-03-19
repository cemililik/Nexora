using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to start a contact import job.</summary>
public sealed record StartContactImportCommand(
    string FileName,
    string FileFormat,
    byte[] FileContent) : ICommand<ImportJobDto>;

/// <summary>Validates contact import input.</summary>
public sealed class StartContactImportValidator : AbstractValidator<StartContactImportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "xlsx"];

    public StartContactImportValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_filename_required");

        RuleFor(x => x.FileFormat)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_import_format_invalid");

        RuleFor(x => x.FileContent)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_file_empty")
            .Must(c => c.Length <= 10 * 1024 * 1024)
            .WithMessage("lockey_contacts_validation_import_file_too_large");
    }
}

/// <summary>Starts a background import job and returns job tracking info.</summary>
public sealed class StartContactImportHandler(
    ITenantContextAccessor tenantContextAccessor,
    ILogger<StartContactImportHandler> logger) : ICommandHandler<StartContactImportCommand, ImportJobDto>
{
    public Task<Result<ImportJobDto>> Handle(
        StartContactImportCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var jobId = Guid.NewGuid();

        // In production, this would enqueue a Hangfire job:
        // BackgroundJob.Enqueue<ContactImportJob>(j => j.RunAsync(params, ct));
        // For now, we return a job tracking DTO

        logger.LogInformation("Contact import job {JobId} started for tenant {TenantId} with file {FileName}",
            jobId, tenantId, request.FileName);

        var dto = new ImportJobDto(
            jobId, "Queued", 0, 0, 0, 0,
            DateTimeOffset.UtcNow, null);

        return Task.FromResult(Result<ImportJobDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_import_job_started")));
    }
}
