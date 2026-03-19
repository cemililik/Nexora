using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Records user actions for audit and compliance purposes.</summary>
public sealed class AuditLog : Entity<AuditLogId>
{
    public UserId UserId { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Action { get; private set; } = default!;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? Details { get; private set; }

    private AuditLog() { }

    /// <summary>Creates an audit log entry.</summary>
    public static AuditLog Create(
        UserId userId,
        TenantId tenantId,
        string action,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null)
    {
        return new AuditLog
        {
            Id = AuditLogId.New(),
            UserId = userId,
            TenantId = tenantId,
            Action = action,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTimeOffset.UtcNow,
            Details = details
        };
    }
}
