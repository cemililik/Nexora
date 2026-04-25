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
            file = JsonSerializer.Deserialize<JsonLicenseFile>(fileBytes.Span);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "License validator: malformed JSON.");
            return Task.FromResult(LicenseValidationResult.Invalid(
                "lockey_licensing_validation_malformed_json",
                $"Malformed license JSON: {ex.Message}"));
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
    /// fixtures.
    /// </summary>
    public sealed record JsonLicenseFile
    {
        public string? LicenseId { get; init; }
        public string? Tier { get; init; }
        public DateTime ValidFromUtc { get; init; }
        public DateTime ValidUntilUtc { get; init; }
        public IReadOnlyList<string>? Modules { get; init; }
    }
}
