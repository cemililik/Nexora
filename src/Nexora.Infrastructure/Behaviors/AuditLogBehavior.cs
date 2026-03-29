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
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AuditLogBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
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
        catch (Exception ex)
        {
            // Audit config check failed (e.g., Dapr/cache unavailable) — skip audit, don't block
            logger.LogError(ex, "AUDIT CONFIG CHECK FAILED for {Module}.{Operation}: {ErrorMessage}", module, operation, ex.Message);
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
                ? (false, handlerException?.GetType().Name)
                : DetermineOutcome(response);

            string? entityType = null;
            if (request is IAuditable auditable)
                entityType = auditable.AuditEntityType;

            var tenantId = tenantContextAccessor.Current.TenantId;
            var operationType = requestKind == RequestKind.Query
                ? OperationType.Read
                : OperationType.Action;

            var entry = new AuditEntry(
                Id: Guid.NewGuid(),
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
                EntityType: entityType,
                EntityId: null,
                BeforeState: null,
                AfterState: null,
                Changes: null,
                Metadata: null,
                Timestamp: DateTimeOffset.UtcNow);

            await auditStore.SaveAsync(entry, cancellationToken);
            logger.LogInformation("Audit entry saved for {Module}.{Operation} success={IsSuccess}", module, operation, isSuccess);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AUDIT SAVE FAILED for {Module}.{Operation}: {ErrorMessage}", module, operation, ex.Message);
        }

        // Re-throw the handler exception so the pipeline continues normally
        if (handlerFailed)
            throw handlerException!;

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
        if (response is null)
            return (true, null);

        // Handle Result (non-generic)
        if (response is Result result)
            return (result.IsSuccess, result.Error?.Message.Key);

        // Handle Result<T>
        var responseType = response.GetType();
        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var isSuccessProp = responseType.GetProperty(nameof(Result.IsSuccess));
            var errorProp = responseType.GetProperty(nameof(Result.Error));

            if (isSuccessProp is not null)
            {
                var isSuccess = (bool)isSuccessProp.GetValue(response)!;
                string? errorKey = null;

                if (!isSuccess && errorProp is not null)
                {
                    var error = errorProp.GetValue(response) as Error;
                    errorKey = error?.Message.Key;
                }

                return (isSuccess, errorKey);
            }
        }

        return (true, null);
    }

    /// <summary>Classifies MediatR requests into commands, queries, or other types.</summary>
    private enum RequestKind
    {
        Command,
        Query,
        Other
    }
}
