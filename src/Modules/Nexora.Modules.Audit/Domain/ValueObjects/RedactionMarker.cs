namespace Nexora.Modules.Audit.Domain.ValueObjects;

/// <summary>
/// Serialized marker written into audit payload columns when PII is redacted for GDPR erasure.
/// The leading underscore on each property is part of the on-the-wire contract — downstream
/// consumers (audit viewer, legal export) look for these exact keys to recognize a redacted row.
/// </summary>
/// <param name="_redacted">Always <c>true</c> — identifies this payload as redacted.</param>
/// <param name="_reason">Reason code (currently always <c>gdpr_erasure</c>).</param>
/// <param name="_erasedAtUtc">ISO-8601 round-trip timestamp of the erasure.</param>
/// <param name="_erasedByUserId">Dashed GUID ("D" format) of the actor who initiated the erasure.</param>
public sealed record RedactionMarker(
    bool _redacted,
    string _reason,
    string _erasedAtUtc,
    string _erasedByUserId);
