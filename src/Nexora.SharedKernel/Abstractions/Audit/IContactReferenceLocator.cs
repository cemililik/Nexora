namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// T-010 (ADR-0026): each module that writes audit entries with embedded
/// contact references registers an <see cref="IContactReferenceLocator"/>.
/// The locator owns the knowledge of where contact IDs may appear in
/// that module's JSON payload columns (<c>BeforeState</c>, <c>AfterState</c>,
/// <c>Changes</c>) and how to redact them in place — keeping the audit
/// module generic and avoiding the false-positive risk of full-table
/// regex scans rejected in ADR-0026.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module-owned redaction.</b> Each module knows its own JSON shape
/// (e.g. CRM Lead's <c>{ "assignedTo": { "contactId": "..." } }</c>);
/// the locator's <see cref="Redact"/> walks that shape and replaces
/// any contact-bearing branch with the redaction marker. Returning
/// <see langword="null"/> means "nothing to redact in this payload" —
/// callers leave the existing payload untouched.
/// </para>
/// <para>
/// <b>Architecture-test enforced.</b> A test in
/// <c>Nexora.Architecture.Tests</c> asserts that any module whose
/// integration events expose a <c>ContactId</c> property (other than the
/// Contacts module itself) registers an <see cref="IContactReferenceLocator"/>
/// — see <c>ContactReferenceLocatorCoverageTests</c>. Without that gate,
/// new modules would silently leak PII into the audit payload columns.
/// </para>
/// </remarks>
public interface IContactReferenceLocator
{
    /// <summary>
    /// Module slug — must equal <c>AuditEntry.Module</c> for entries this
    /// locator can redact. The scan job uses this to filter the audit
    /// table down to entries the locator actually understands; mismatches
    /// are skipped silently.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Returns a redacted copy of <paramref name="jsonPayload"/> when the
    /// payload references <paramref name="contactId"/>; returns
    /// <see langword="null"/> when nothing matches (caller leaves the
    /// existing payload in place). MUST be deterministic + idempotent —
    /// re-running on already-redacted payload returns either the same
    /// redacted bytes or null.
    /// </summary>
    /// <param name="jsonPayload">
    /// One of <c>AuditEntry.BeforeState</c> / <c>AfterState</c> /
    /// <c>Changes</c>. May be <see langword="null"/> — locators MUST handle
    /// the null case (return <see langword="null"/>) without throwing.
    /// </param>
    /// <param name="contactId">
    /// The erased contact's GUID. Locators search for this value in their
    /// declared payload paths.
    /// </param>
    string? Redact(string? jsonPayload, Guid contactId);
}
