using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Commands;

/// <summary>Auth-related events captured directly from the frontend or an identity provider webhook.</summary>
public enum AuthEventType
{
    /// <summary>Successful interactive login.</summary>
    Login,
    /// <summary>User-initiated logout or token invalidation.</summary>
    Logout,
    /// <summary>User changed their password.</summary>
    PasswordChange,
    /// <summary>Access token was refreshed via the refresh-token grant.</summary>
    TokenRefresh,
    /// <summary>A login attempt was rejected (wrong password, locked account, etc.).</summary>
    LoginFailed
}

/// <summary>
/// Records an authentication-related event in the audit log. Intended to be called by the admin
/// and portal frontends post-login / pre-logout, and by any future Keycloak event listener webhook.
/// </summary>
public sealed record RecordAuthEventCommand(
    AuthEventType EventType,
    bool IsSuccess,
    string? Metadata) : ICommand;

/// <summary>Validates auth event recording input — EventType must be a known value.</summary>
public sealed class RecordAuthEventValidator : AbstractValidator<RecordAuthEventCommand>
{
    public RecordAuthEventValidator()
    {
        RuleFor(x => x.EventType)
            .IsInEnum().WithMessage("lockey_audit_validation_auth_event_type_invalid");
    }
}

/// <summary>
/// Writes the auth event straight to <see cref="IAuditStore"/> — bypassing the audit
/// behavior's config gate, because auth events must always be retained regardless of
/// per-module audit configuration.
/// </summary>
public sealed class RecordAuthEventHandler(
    IAuditStore auditStore,
    IAuditContext auditContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RecordAuthEventHandler> logger) : ICommandHandler<RecordAuthEventCommand>
{
    /// <summary>Persists the auth event as an immutable audit entry with module="identity".</summary>
    public async Task<Result> Handle(RecordAuthEventCommand request, CancellationToken cancellationToken)
    {
        string tenantId;
        try
        {
            tenantId = tenantContextAccessor.Current.TenantId;
        }
        catch (InvalidOperationException)
        {
            // Logout/LoginFailed can arrive without a tenant context — use empty tenant slot.
            tenantId = string.Empty;
        }

        var entry = new AuditEntry(
            Id: AuditEntryId.New(),
            TenantId: tenantId,
            Module: "identity",
            Operation: $"Auth.{request.EventType}",
            OperationType: OperationType.Action,
            UserId: auditContext.UserId,
            UserEmail: auditContext.UserEmail,
            IpAddress: auditContext.IpAddress,
            UserAgent: auditContext.UserAgent,
            CorrelationId: auditContext.CorrelationId,
            IsSuccess: request.IsSuccess,
            ErrorKey: null,
            EntityType: "AuthSession",
            EntityId: auditContext.UserId?.ToString(),
            BeforeState: null,
            AfterState: null,
            Changes: null,
            Metadata: request.Metadata,
            Timestamp: DateTimeOffset.UtcNow);

        try
        {
            await auditStore.SaveAsync(entry, cancellationToken);
            logger.LogInformation(
                "Auth event recorded: {EventType} success={IsSuccess} userId={UserId}",
                request.EventType, request.IsSuccess, auditContext.UserId);
            return Result.Success(LocalizedMessage.Of("lockey_audit_auth_event_recorded"));
        }
        // [ADR] Audit write failures must never block the caller's auth flow — log and degrade.
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record auth event {EventType}", request.EventType);
            return Result.Failure(LocalizedMessage.Of("lockey_audit_error_auth_event_failed"));
        }
    }
}
