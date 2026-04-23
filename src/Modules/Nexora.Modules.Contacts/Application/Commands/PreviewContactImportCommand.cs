using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Parses the first 5 rows of a previously uploaded import file so the admin UI
/// can render a column-mapping table. This command is stateless — it does NOT
/// create an <c>ImportJob</c> record.
/// </summary>
public sealed record PreviewContactImportCommand(
    string StorageKey,
    string FileFormat) : ICommand<ContactImportPreviewDto>;
