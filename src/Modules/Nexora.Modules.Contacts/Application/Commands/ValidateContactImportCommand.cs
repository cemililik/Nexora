using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Pre-flight validation for a contact import. Parses all rows, applies the supplied
/// column mapping, and returns row-level error findings (capped at 100).
/// Does NOT perform duplicate detection — that is deferred to the background job.
/// </summary>
public sealed record ValidateContactImportCommand(
    string StorageKey,
    string FileFormat,
    IReadOnlyDictionary<string, string> ColumnMapping) : ICommand<ContactImportValidationDto>;
