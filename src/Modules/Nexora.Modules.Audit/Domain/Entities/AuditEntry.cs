using Nexora.Modules.Audit.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Audit.Domain.Entities;

/// <summary>
/// Immutable audit log entry. Inherits Entity (not AuditableEntity) because
/// audit entries are append-only and never soft-deleted.
/// </summary>
public sealed class AuditEntry : Entity<AuditEntryId>
{
    public string TenantId { get; private set; } = default!;
    public string Module { get; private set; } = default!;
    public string Operation { get; private set; } = default!;
    public string OperationType { get; private set; } = default!;
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorKey { get; private set; }
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? BeforeState { get; private set; }
    public string? AfterState { get; private set; }
    public string? Changes { get; private set; }
    public string? Metadata { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    private AuditEntry() { }

    /// <summary>Creates a new immutable audit entry from the given parameters.</summary>
    public static AuditEntry Create(
        string tenantId,
        string module,
        string operation,
        string operationType,
        Guid? userId,
        string? userEmail,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        bool isSuccess,
        string? errorKey,
        string? entityType,
        string? entityId,
        string? beforeState,
        string? afterState,
        string? changes,
        string? metadata,
        DateTimeOffset timestamp)
    {
        return new AuditEntry
        {
            Id = AuditEntryId.New(),
            TenantId = tenantId,
            Module = module,
            Operation = operation,
            OperationType = operationType,
            UserId = userId,
            UserEmail = userEmail,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            IsSuccess = isSuccess,
            ErrorKey = errorKey,
            EntityType = entityType,
            EntityId = entityId,
            BeforeState = beforeState,
            AfterState = afterState,
            Changes = changes,
            Metadata = metadata,
            Timestamp = timestamp
        };
    }
}
