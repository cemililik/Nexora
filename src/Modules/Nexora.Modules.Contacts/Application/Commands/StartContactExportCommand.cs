using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Command to start a contact export job. Persists an <c>ExportJob</c>,
/// enqueues a Hangfire background job, and returns the client-facing DTO.
/// </summary>
public sealed record StartContactExportCommand(
    string Format,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyList<Guid>? CustomFieldIds = null,
    string? StatusFilter = null,
    string? TypeFilter = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    string? DateField = null) : ICommand<ExportJobDto>;

/// <summary>Serialization shape for <c>ExportJob.FiltersJson</c>.</summary>
public sealed record ExportFiltersPayload(
    string? StatusFilter,
    string? TypeFilter,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? DateField);

/// <summary>Serialization shape for <c>ExportJob.FieldsJson</c>.</summary>
public sealed record ExportFieldsPayload(
    IReadOnlyList<string>? Fields,
    IReadOnlyList<Guid>? CustomFieldIds);
