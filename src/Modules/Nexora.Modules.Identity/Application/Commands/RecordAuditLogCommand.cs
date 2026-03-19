using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to record an audit log entry (e.g. login, logout, password change).</summary>
public sealed record RecordAuditLogCommand(
    Guid UserId,
    string Action,
    string? IpAddress = null,
    string? UserAgent = null,
    string? Details = null) : ICommand;

/// <summary>Validates audit log recording input.</summary>
public sealed class RecordAuditLogValidator : AbstractValidator<RecordAuditLogCommand>
{
    public RecordAuditLogValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("lockey_identity_validation_audit_action_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_audit_action_max_length");
    }
}

/// <summary>Creates an audit log entry in the database.</summary>
public sealed class RecordAuditLogHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RecordAuditLogHandler> logger) : ICommandHandler<RecordAuditLogCommand>
{
    public async Task<Result> Handle(
        RecordAuditLogCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.UserId);

        var auditLog = AuditLog.Create(
            userId, tenantId, request.Action,
            request.IpAddress, request.UserAgent, request.Details);

        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Audit log recorded: {Action} by user {UserId}", request.Action, request.UserId);

        return Result.Success(new LocalizedMessage("lockey_identity_audit_log_recorded"));
    }
}
