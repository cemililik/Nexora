namespace Nexora.Modules.Audit.Application.DTOs;

/// <summary>Represents a single auditable operation within a module.</summary>
/// <param name="Operation">The operation name (e.g., "CreateUser" for commands, "Query.GetUsers" for queries).</param>
/// <param name="OperationType">The category of operation (Create, Update, Delete, Action, Read).</param>
/// <param name="SourceKind">Whether this operation originates from a "Command" or "Query".</param>
public sealed record AuditableOperationDto(string Operation, string OperationType, string SourceKind);
