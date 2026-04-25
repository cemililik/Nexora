using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Licensing;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-014: minimal default <see cref="ILicenseValidator"/> — parses the
/// license file as a <see cref="JsonLicenseFile"/>, checks
/// <c>ValidUntilUtc</c>, and returns a <see cref="LicenseSnapshot"/>.
/// No signature verification at this layer — that lives in the NMP track
/// where the issuer's public key is provisioned at deploy time. Until
/// then this validator is enough to exercise the reload service end-to-end
/// (T-014's AC is the hot-reload mechanism, not the license format itself).
/// </summary>
public sealed class JsonLicenseValidator(
    ILogger<JsonLicenseValidator> logger,
    TimeProvider? timeProvider = null) : ILicenseValidator
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        // Accept both PascalCase (the property names declared on the
        // record) and camelCase (the convention most issuers will emit).
        // Without this flag a camelCase license file deserialises every
        // property to its default value and the missing-fields branch
        // fires — a confusing failure mode for what is otherwise a
        // valid payload.
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public Task<LicenseValidationResult> ValidateAsync(ReadOnlyMemory<byte> fileBytes, CancellationToken ct)
    {
        if (fileBytes.IsEmpty)
            return Task.FromResult(LicenseValidationResult.Invalid(
                "lockey_licensing_validation_empty_file",
                "License file is empty."));

        JsonLicenseFile? file;
        try
        {
            file = JsonSerializer.Deserialize<JsonLicenseFile>(fileBytes.Span, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            // Pass the exception via the logger but ship a fixed
            // classification string in ErrorReason so downstream
            // integration-event consumers do not see raw parser output
            // (which can leak file-position offsets and reveal internals).
            logger.LogWarning(ex, "License validator: malformed JSON.");
            return Task.FromResult(LicenseValidationResult.Invalid(
                "lockey_licensing_validation_malformed_json",
                "MalformedLicenseJson"));
        }

        if (file is null
            || string.IsNullOrWhiteSpace(file.LicenseId)
            || string.IsNullOrWhiteSpace(file.Tier))
        {
            return Task.FromResult(LicenseValidationResult.Invalid(
                "lockey_licensing_validation_missing_fields",
                "License file is missing required fields (LicenseId, Tier)."));
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // Pre-active license: ValidFromUtc is in the future. Refuse rather
        // than letting the call site silently activate a license that the
        // issuer scheduled for tomorrow. Exclusive comparison: a license
        // whose ValidFromUtc equals nowUtc is treated as already-active.
        if (file.ValidFromUtc > nowUtc)
        {
            return Task.FromResult(LicenseValidationResult.Invalid(
                "lockey_licensing_validation_not_yet_active",
                $"License not active until {file.ValidFromUtc:O}."));
        }

        if (file.ValidUntilUtc <= nowUtc)
        {
            return Task.FromResult(LicenseValidationResult.Invalid(
                "lockey_licensing_validation_expired",
                $"License expired on {file.ValidUntilUtc:O}."));
        }

        var snapshot = new LicenseSnapshot
        {
            LicenseId = file.LicenseId,
            Tier = file.Tier,
            ValidFromUtc = file.ValidFromUtc,
            ValidUntilUtc = file.ValidUntilUtc,
            Modules = file.Modules ?? [],
            LoadedAtUtc = nowUtc,
        };
        return Task.FromResult(LicenseValidationResult.Valid(snapshot));
    }

    /// <summary>
    /// Wire shape for the on-prem license file. Public so issuers can
    /// reuse the type when generating files; tests use it to forge
    /// fixtures. Property-name matching is case-insensitive on read so
    /// camelCase JSON is accepted as well as PascalCase.
    /// </summary>
    public sealed record JsonLicenseFile
    {
        /// <summary>Globally-unique license identifier — projects to <c>LicenseSnapshot.LicenseId</c>.</summary>
        public string? LicenseId { get; init; }

        /// <summary>Tier slug (e.g. <c>"professional"</c>, <c>"enterprise"</c>) — gates module access.</summary>
        public string? Tier { get; init; }

        /// <summary>UTC instant the license becomes active. <see cref="DateTimeKind.Utc"/> recommended.</summary>
        public DateTime ValidFromUtc { get; init; }

        /// <summary>UTC instant the license expires (exclusive — equality with <c>now</c> is past-expiry).</summary>
        public DateTime ValidUntilUtc { get; init; }

        /// <summary>Module slugs the license entitles. Null/empty means platform-only (no Tier-2 modules).</summary>
        public IReadOnlyList<string>? Modules { get; init; }
    }
}
