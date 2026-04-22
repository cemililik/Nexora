using System.Runtime.ExceptionServices;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that creates audit log entries for command and query executions.
/// Commands are audited by default (enabled unless explicitly disabled in settings).
/// Queries are auditable but disabled by default to prevent performance overhead.
/// </summary>
public sealed class AuditLogBehavior<TRequest, TResponse>(
    IAuditContext auditContext,
    IAuditConfigService configService,
    IAuditStore auditStore,
    IAuditStateCapture stateCapture,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AuditLogBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Intercepts command/query execution to create audit log entries.</summary>
    /// <remarks>
    /// Audit failures NEVER block business logic. If config check or audit write fails,
    /// the request executes normally and the failure is logged.
    /// </remarks>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestKind = ClassifyRequest();

        // Only audit commands and queries, skip other request types
        if (requestKind == RequestKind.Other)
            return await next();

        logger.LogDebug("AuditLogBehavior triggered for {RequestType} ({RequestKind})", typeof(TRequest).Name, requestKind);

        var (module, operation) = ExtractModuleAndOperation(request, requestKind);
        var defaultEnabled = requestKind == RequestKind.Command;

        bool auditEnabled;
        try
        {
            auditEnabled = await configService.IsEnabledAsync(module, operation, cancellationToken, defaultEnabled);
        }
        // [ADR] Architectural exemption: Audit logging must never block business logic.
        // These catch blocks intentionally swallow exceptions to ensure that audit
        // infrastructure failures (cache unavailable, DB timeout) do not impact
        // the business operation being audited. See CODING_STANDARDS.md catch(Exception) rule.
        catch (Exception ex)
        {
            // Audit config check failed (e.g., Dapr/cache unavailable) — skip audit, don't block
            logger.LogError(ex, "Audit config check failed for {Module}.{Operation}", module, operation);
            return await next();
        }

        logger.LogDebug("Audit config result for {Module}.{Operation}: enabled={AuditEnabled}", module, operation, auditEnabled);

        if (!auditEnabled)
            return await next();

        // Execute the handler — catch exceptions to still audit failed operations
        TResponse response;
        bool handlerFailed = false;
        Exception? handlerException = null;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            // Handler threw — we still want to audit this as a failed operation
            handlerFailed = true;
            handlerException = ex;
            response = default!;
        }

        // Build and save audit entry — failures must never block the business response
        try
        {
            var (isSuccess, errorKey) = handlerFailed
                ? (false, (string?)"lockey_audit_handler_exception")
                : DetermineOutcome(response);

            string? entityType = null;
            OperationType? explicitOperationType = null;
            if (request is IAuditable auditable)
            {
                entityType = auditable.AuditEntityType;
                explicitOperationType = auditable.AuditOperationType;
            }

            var tenantId = tenantContextAccessor.Current.TenantId;
            var operationType = explicitOperationType
                ?? (requestKind == RequestKind.Query
                    ? OperationType.Read
                    : DeriveOperationType(operation));

            var (beforeJson, afterJson, changesJson, inferredEntityType, inferredEntityId) =
                SerializeCapturedChanges(stateCapture.Changes);

            var entry = new AuditEntry(
                Id: AuditEntryId.New(),
                TenantId: tenantId,
                Module: module,
                Operation: operation,
                OperationType: operationType,
                UserId: auditContext.UserId,
                UserEmail: auditContext.UserEmail,
                IpAddress: auditContext.IpAddress,
                UserAgent: auditContext.UserAgent,
                CorrelationId: auditContext.CorrelationId,
                IsSuccess: isSuccess,
                ErrorKey: errorKey,
                EntityType: entityType ?? inferredEntityType,
                EntityId: inferredEntityId,
                BeforeState: beforeJson,
                AfterState: afterJson,
                Changes: changesJson,
                Metadata: null,
                Timestamp: DateTimeOffset.UtcNow);

            await auditStore.SaveAsync(entry, cancellationToken);
            logger.LogInformation(
                "Audit entry saved for {Module}.{Operation} success={IsSuccess} entities={EntityCount}",
                module, operation, isSuccess, stateCapture.Changes.Count);

            // Clear so the next request/scope starts clean (scoped DI should already isolate, but be defensive).
            stateCapture.Clear();
        }
        // [ADR] Architectural exemption: Audit logging must never block business logic.
        // These catch blocks intentionally swallow exceptions to ensure that audit
        // infrastructure failures (cache unavailable, DB timeout) do not impact
        // the business operation being audited. See CODING_STANDARDS.md catch(Exception) rule.
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit save failed for {Module}.{Operation}", module, operation);
        }

        // Re-throw the handler exception preserving the original stack trace
        if (handlerFailed)
            ExceptionDispatchInfo.Capture(handlerException!).Throw();

        return response;
    }

    /// <summary>Classifies the request as a command, query, or other request type.</summary>
    private static RequestKind ClassifyRequest()
    {
        var requestType = typeof(TRequest);

        if (typeof(ICommand).IsAssignableFrom(requestType))
            return RequestKind.Command;

        if (requestType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
            return RequestKind.Command;

        if (requestType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            return RequestKind.Query;

        return RequestKind.Other;
    }

    /// <summary>Extracts module name and operation from the request type or IAuditable interface.</summary>
    private static (string Module, string Operation) ExtractModuleAndOperation(TRequest request, RequestKind requestKind)
    {
        if (request is IAuditable auditable)
            return (auditable.AuditModule, auditable.AuditOperation);

        var requestType = typeof(TRequest);
        var module = ExtractModuleFromNamespace(requestType.Namespace);

        var operation = requestKind switch
        {
            RequestKind.Query => ExtractQueryOperationName(requestType.Name),
            _ => requestType.Name.EndsWith("Command", StringComparison.Ordinal)
                ? requestType.Name[..^"Command".Length]
                : requestType.Name
        };

        return (module, operation);
    }

    /// <summary>
    /// Extracts query operation name by removing "Query" suffix and adding "Query." prefix.
    /// Example: "GetUsersQuery" → "Query.GetUsers"
    /// </summary>
    private static string ExtractQueryOperationName(string className)
    {
        var baseName = className.EndsWith("Query", StringComparison.Ordinal)
            ? className[..^"Query".Length]
            : className;

        return $"Query.{baseName}";
    }

    /// <summary>Extracts the module name from a namespace like "Nexora.Modules.Contacts.Application.Commands".</summary>
    private static string ExtractModuleFromNamespace(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
            return "Unknown";

        const string prefix = "Nexora.Modules.";
        var startIndex = ns.IndexOf(prefix, StringComparison.Ordinal);
        if (startIndex < 0)
            return "Unknown";

        var moduleStart = startIndex + prefix.Length;
        var dotIndex = ns.IndexOf('.', moduleStart);

        var moduleName = dotIndex < 0
            ? ns[moduleStart..]
            : ns[moduleStart..dotIndex];

        return moduleName.ToLowerInvariant();
    }

    /// <summary>Determines success/failure and error key from the response.</summary>
    private static (bool IsSuccess, string? ErrorKey) DetermineOutcome(TResponse response)
    {
        if (response is IOperationResult result)
            return (result.IsSuccess, result.Error?.Message.Key);

        return (true, null);
    }

    /// <summary>Derives the operation type from the operation name prefix for commands.</summary>
    private static OperationType DeriveOperationType(string operation)
    {
        if (operation.StartsWith("Create", StringComparison.Ordinal))
            return OperationType.Create;
        if (operation.StartsWith("Update", StringComparison.Ordinal))
            return OperationType.Update;
        if (operation.StartsWith("Delete", StringComparison.Ordinal) ||
            operation.StartsWith("Remove", StringComparison.Ordinal))
            return OperationType.Delete;

        return OperationType.Action;
    }

    /// <summary>Classifies MediatR requests into commands, queries, or other types.</summary>
    private enum RequestKind
    {
        Command,
        Query,
        Other
    }

    /// <summary>
    /// Serializes the entity snapshots collected by <see cref="IAuditStateCapture"/> into the
    /// BeforeState / AfterState / Changes JSON fields. When exactly one entity was touched we
    /// also surface its type and id so the audit entry is filterable without parsing JSON.
    /// </summary>
    private static (string? Before, string? After, string? Changes, string? EntityType, string? EntityId)
        SerializeCapturedChanges(IReadOnlyList<CapturedEntityChange> changes)
    {
        if (changes.Count == 0)
            return (null, null, null, null, null);

        var before = changes
            .Where(c => c.Before.Count > 0)
            .Select(c => new { entityType = c.EntityType, entityId = c.EntityId, data = c.Before })
            .ToArray();

        var after = changes
            .Where(c => c.After.Count > 0)
            .Select(c => new { entityType = c.EntityType, entityId = c.EntityId, data = c.After })
            .ToArray();

        var delta = changes
            .Select(c => new { entityType = c.EntityType, entityId = c.EntityId, kind = c.Kind.ToString(), data = c.Delta })
            .ToArray();

        var beforeJson = before.Length > 0 ? JsonSerializer.Serialize(before, _jsonOptions) : null;
        var afterJson = after.Length > 0 ? JsonSerializer.Serialize(after, _jsonOptions) : null;
        var changesJson = JsonSerializer.Serialize(delta, _jsonOptions);

        string? entityType = null;
        string? entityId = null;
        if (changes.Count == 1)
        {
            entityType = changes[0].EntityType;
            entityId = changes[0].EntityId;
        }

        return (beforeJson, afterJson, changesJson, entityType, entityId);
    }
}
