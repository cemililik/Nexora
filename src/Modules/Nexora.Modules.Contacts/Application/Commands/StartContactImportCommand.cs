using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Command to start a contact import job from a previously uploaded file.
/// <paramref name="ColumnMapping"/> maps source header → target Contact field
/// (or <c>__skip__</c>). When null, the job treats source headers as identity mappings.
/// </summary>
public sealed record StartContactImportCommand(
    string FileName,
    string FileFormat,
    string StorageKey,
    IReadOnlyDictionary<string, string>? ColumnMapping = null) : ICommand<ImportJobDto>;
