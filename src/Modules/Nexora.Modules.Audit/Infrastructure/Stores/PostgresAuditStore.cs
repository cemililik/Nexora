using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Audit;
using AuditEntryEntity = Nexora.Modules.Audit.Domain.Entities.AuditEntry;
using AuditEntryRecord = Nexora.SharedKernel.Abstractions.Audit.AuditEntry;

namespace Nexora.Modules.Audit.Infrastructure.Stores;

/// <summary>PostgreSQL-backed implementation of IAuditStore using EF Core.</summary>
public sealed class PostgresAuditStore(
    AuditDbContext dbContext,
    ILogger<PostgresAuditStore> logger) : IAuditStore
{
    /// <inheritdoc />
    public async Task SaveAsync(AuditEntryRecord entry, CancellationToken ct)
    {
        var entity = AuditEntryEntity.Create(
            tenantId: entry.TenantId,
            module: entry.Module,
            operation: entry.Operation,
            operationType: entry.OperationType.ToString(),
            userId: entry.UserId,
            userEmail: entry.UserEmail,
            ipAddress: entry.IpAddress,
            userAgent: entry.UserAgent,
            correlationId: entry.CorrelationId,
            isSuccess: entry.IsSuccess,
            errorKey: entry.ErrorKey,
            entityType: entry.EntityType,
            entityId: entry.EntityId,
            beforeState: entry.BeforeState,
            afterState: entry.AfterState,
            changes: entry.Changes,
            metadata: entry.Metadata,
            timestamp: entry.Timestamp);

        dbContext.AuditEntries.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Audit entry persisted for {Module}/{Operation} by user {UserId} in tenant {TenantId}",
            entry.Module, entry.Operation, entry.UserId, entry.TenantId);
    }
}
