using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record CreateReportDefinitionCommand(
    string Name,
    string? Description,
    string Module,
    string? Category,
    string QueryText,
    string? Parameters,
    string DefaultFormat) : ICommand<ReportDefinitionDto>;

public sealed class CreateReportDefinitionValidator : AbstractValidator<CreateReportDefinitionCommand>
{
    public CreateReportDefinitionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("lockey_validation_required")
            .MaximumLength(200).WithMessage("lockey_validation_max_length");
        RuleFor(x => x.Module).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.QueryText).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.DefaultFormat).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class CreateReportDefinitionHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateReportDefinitionHandler> logger) : ICommandHandler<CreateReportDefinitionCommand, ReportDefinitionDto>
{
    public async Task<Result<ReportDefinitionDto>> Handle(CreateReportDefinitionCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);

        if (!SqlQueryValidator.IsValid(request.QueryText, out var sqlError))
            return Result<ReportDefinitionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_query", new() { ["reason"] = sqlError! }));

        if (!Enum.TryParse<ReportFormat>(request.DefaultFormat, true, out var format))
            return Result<ReportDefinitionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_format"));

        var definition = ReportDefinition.Create(
            tenantId, orgId, request.Name, request.Description,
            request.Module, request.Category, request.QueryText,
            request.Parameters, format);

        await dbContext.ReportDefinitions.AddAsync(definition, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report definition {DefinitionId} created for tenant {TenantId}",
            definition.Id, tenantId);

        var dto = MapToDto(definition);
        return Result<ReportDefinitionDto>.Success(dto,
            LocalizedMessage.Of("lockey_reporting_definition_created"));
    }

    private static ReportDefinitionDto MapToDto(ReportDefinition d) => new(
        d.Id.Value, d.Name, d.Description, d.Module, d.Category,
        d.QueryText, d.Parameters, d.DefaultFormat.ToString(),
        d.IsActive, d.CreatedAt, d.CreatedBy);
}
