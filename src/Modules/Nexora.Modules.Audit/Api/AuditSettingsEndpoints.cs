using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Audit.Application.Commands;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Api;

/// <summary>Minimal API endpoints for audit settings management.</summary>
public static class AuditSettingsEndpoints
{
    /// <summary>Maps audit settings list, update, and auditable operations discovery endpoints.</summary>
    public static void MapAuditSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/settings")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAuditSettingsQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<AuditSettingDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<IReadOnlyList<AuditSettingDto>>.Fail(result.Error!));
        });

        group.MapPut("/", async (UpdateAuditSettingCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<AuditSettingDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<AuditSettingDto>.Fail(result.Error!));
        });

        group.MapPut("/bulk", async (BulkUpdateAuditSettingsCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<List<AuditSettingDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<List<AuditSettingDto>>.Fail(result.Error!));
        });

        group.MapGet("/operations", (IReadOnlyList<IModule> modules) =>
        {
            var operations = DiscoverAuditableOperations(modules);
            return Results.Ok(ApiEnvelope<List<AuditableModuleDto>>.Success(operations));
        });
    }

    /// <summary>
    /// Scans all loaded assemblies for types implementing ICommand, ICommand&lt;T&gt;, or IQuery&lt;T&gt;
    /// and groups them by module. Also includes hardcoded auth events.
    /// </summary>
    private static List<AuditableModuleDto> DiscoverAuditableOperations(IReadOnlyList<IModule> modules)
    {
        var moduleTypes = modules
            .Select(m => m.GetType().Assembly)
            .Distinct()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException) { return []; }
            })
            .Where(t => t is { IsInterface: false, IsAbstract: false } && (ImplementsCommand(t) || ImplementsQuery(t)))
            .ToList();

        var grouped = new Dictionary<string, List<AuditableOperationDto>>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in moduleTypes)
        {
            var moduleName = ExtractModuleName(type.Namespace);
            var isQuery = ImplementsQuery(type);

            var operationName = isQuery
                ? ExtractQueryOperationName(type.Name)
                : ExtractCommandOperationName(type.Name);

            var operationType = isQuery
                ? "Read"
                : DetermineOperationType(operationName);

            var sourceKind = isQuery ? "Query" : "Command";

            if (!grouped.TryGetValue(moduleName, out var list))
            {
                list = [];
                grouped[moduleName] = list;
            }

            list.Add(new AuditableOperationDto(operationName, operationType, sourceKind));
        }

        // Sort operations within each module and return
        return grouped
            .OrderBy(g => g.Key)
            .Select(g => new AuditableModuleDto(
                g.Key,
                g.Value.OrderBy(o => o.Operation).ToList()))
            .ToList();
    }

    /// <summary>Checks whether a type implements ICommand or ICommand&lt;T&gt;.</summary>
    private static bool ImplementsCommand(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i == typeof(ICommand) ||
            (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));
    }

    /// <summary>Checks whether a type implements IQuery&lt;T&gt;.</summary>
    private static bool ImplementsQuery(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
    }

    /// <summary>Extracts the module name from the namespace (e.g., Nexora.Modules.Identity.Application.Commands → identity).</summary>
    private static string ExtractModuleName(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
            return "unknown";

        // Namespace pattern: Nexora.Modules.{ModuleName}.Application.Commands
        var parts = ns.Split('.');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "Modules", StringComparison.Ordinal) && i + 1 < parts.Length)
            {
                return parts[i + 1].ToLowerInvariant();
            }
        }

        return "unknown";
    }

    /// <summary>Extracts a human-readable operation name by removing the "Command" suffix.</summary>
    private static string ExtractCommandOperationName(string className)
    {
        return className.EndsWith("Command", StringComparison.Ordinal)
            ? className[..^7]
            : className;
    }

    /// <summary>
    /// Extracts query operation name by removing "Query" suffix and adding "Query." prefix.
    /// Example: "GetUsersQuery" → "Query.GetUsers"
    /// </summary>
    private static string ExtractQueryOperationName(string className)
    {
        var baseName = className.EndsWith("Query", StringComparison.Ordinal)
            ? className[..^5]
            : className;

        return $"Query.{baseName}";
    }

    /// <summary>Determines the operation type from the operation name prefix.</summary>
    private static string DetermineOperationType(string operationName)
    {
        if (operationName.StartsWith("Create", StringComparison.Ordinal))
            return "Create";
        if (operationName.StartsWith("Update", StringComparison.Ordinal))
            return "Update";
        if (operationName.StartsWith("Delete", StringComparison.Ordinal))
            return "Delete";
        return "Action";
    }
}
