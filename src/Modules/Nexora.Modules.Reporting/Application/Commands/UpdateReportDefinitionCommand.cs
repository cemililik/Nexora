using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record UpdateReportDefinitionCommand(
    Guid Id,
    string Name,
    string? Description,
    string Module,
    string? Category,
    string QueryText,
    string? Parameters,
    string DefaultFormat) : ICommand<ReportDefinitionDto>;

public sealed class UpdateReportDefinitionValidator : AbstractValidator<UpdateReportDefinitionCommand>
{
    public UpdateReportDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("lockey_validation_required")
            .MaximumLength(200).WithMessage("lockey_validation_max_length");
        RuleFor(x => x.Module).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.QueryText).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.DefaultFormat).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class UpdateReportDefinitionHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateReportDefinitionHandler> logger) : ICommandHandler<UpdateReportDefinitionCommand, ReportDefinitionDto>
{
    public async Task<Result<ReportDefinitionDto>> Handle(UpdateReportDefinitionCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var definitionId = ReportDefinitionId.From(request.Id);

        var definition = await dbContext.ReportDefinitions
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.TenantId == tenantId, ct);

        if (definition is null)
            return Result<ReportDefinitionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_definition_not_found"));

        if (!SqlQueryValidator.IsValid(request.QueryText, out var sqlError))
            return Result<ReportDefinitionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_query", new() { ["reason"] = sqlError! }));

        if (!Enum.TryParse<ReportFormat>(request.DefaultFormat, true, out var format))
            return Result<ReportDefinitionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_format"));

        definition.Update(request.Name, request.Description, request.Module,
            request.Category, request.QueryText, request.Parameters, format);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report definition {DefinitionId} updated for tenant {TenantId}",
            definition.Id, tenantId);

        return Result<ReportDefinitionDto>.Success(
            new ReportDefinitionDto(definition.Id.Value, definition.Name, definition.Description,
                definition.Module, definition.Category, definition.QueryText, definition.Parameters,
                definition.DefaultFormat.ToString(), definition.IsActive, definition.CreatedAt, definition.CreatedBy),
            LocalizedMessage.Of("lockey_reporting_definition_updated"));
    }
}
