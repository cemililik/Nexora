namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Shared constants for the contact import column-mapping contract. The UI and the
/// backend agree on the sentinel string <see cref="SkipSentinel"/>; the frontend mirror
/// lives in <c>src/Clients/nexora-admin/src/modules/contacts/pages/ImportPage.tsx</c>
/// (and must stay in sync).
/// </summary>
public static class ImportColumnMapping
{
    /// <summary>
    /// Sentinel the frontend writes into the mapping for a source column the user chose
    /// to ignore. Stripped by <c>StartContactImportHandler</c> before persisting the
    /// mapping and ignored by <c>ValidateContactImportHandler</c>'s inverse-mapping pass.
    /// </summary>
    public const string SkipSentinel = "__skip__";
}
