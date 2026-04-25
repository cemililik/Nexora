using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Abstractions.Licensing;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-014 (ADR-0030): polling-loop hot-reload watcher for the on-prem
/// license file. Each tick computes the file's SHA-256; on change, calls
/// <see cref="ILicenseValidator.ValidateAsync"/>. On success the
/// <see cref="InMemoryLicenseProvider"/> snapshot is swapped via
/// <see cref="Interlocked.Exchange{T}"/>; on failure the previous snapshot
/// is retained and a <see cref="LicenseRefreshFailedIntegrationEvent"/>
/// fires so the admin sees the rollback.
/// </summary>
/// <remarks>
/// <para>
/// <b>SIGHUP shortcut.</b> On Linux / macOS this service registers a
/// <see cref="PosixSignalRegistration"/> that interrupts the current
/// polling sleep so the next iteration is the forced re-check. Operators
/// can <c>kubectl exec</c> + <c>kill -HUP 1</c> for instant reload after
/// a license upload — the polling loop alone serves the K8s
/// projected-Secret case (where <c>inotify</c> would silently no-op).
/// </para>
/// <para>
/// <b>Why not <see cref="System.IO.FileSystemWatcher"/>?</b> See ADR-0030
/// — the prod target is K8s projected Secret volumes, where the watcher
/// silently fails to fire. A polling baseline is the only mechanism that
/// works uniformly across K8s + bare-metal + Windows + tests.
/// </para>
/// </remarks>
public sealed class LicenseReloadService(
    IOptions<LicenseReloadOptions> options,
    InMemoryLicenseProvider provider,
    ILicenseValidator validator,
    IEventBus eventBus,
    ILogger<LicenseReloadService> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly LicenseReloadOptions _opts = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Sentinel TenantId used for platform-level events emitted by this
    /// hosted service — the license is platform-wide, not tenant-scoped.
    /// </summary>
    public const string PlatformSentinelTenantId = "platform";

    /// <summary>SHA-256 of the file content at the last poll. Empty array on first tick.</summary>
    private byte[] _lastHash = [];

    /// <summary>Cancels the polling delay so the next tick fires immediately. Wired to SIGHUP.</summary>
    private CancellationTokenSource? _interruptCts;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // SIGHUP/SIGUSR1 only on POSIX. On Windows the polling baseline
        // is the entire mechanism; that's by design (Windows is not a
        // supported prod target — see ADR-0003).
        using var hupRegistration = TryRegisterSignal(PosixSignal.SIGHUP);

        // Force one initial reload at startup so the first request after
        // boot already has a snapshot if the license file is on disk.
        await PerformReloadAttemptAsync(stoppingToken);

        var pollInterval = TimeSpan.FromSeconds(_opts.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var interruptCts = new CancellationTokenSource();
            Interlocked.Exchange(ref _interruptCts, interruptCts);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken, interruptCts.Token);
            try
            {
                await Task.Delay(pollInterval, _timeProvider, linked.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // Interrupt fired — fall through to reload-attempt.
                logger.LogDebug("License reload: polling sleep interrupted (SIGHUP / forced).");
            }
            finally
            {
                Interlocked.CompareExchange(ref _interruptCts, null, interruptCts);
            }

            await PerformReloadAttemptAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Performs one reload attempt: hash the file, short-circuit when
    /// unchanged, otherwise call the validator and swap the snapshot.
    /// Exposed so tests can drive a single tick deterministically without
    /// running the polling loop.
    /// </summary>
    public async Task PerformReloadAttemptAsync(CancellationToken ct)
    {
        if (!File.Exists(_opts.LicenseFilePath))
        {
            logger.LogDebug("License reload: file {Path} not present; skipping tick.", _opts.LicenseFilePath);
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_opts.LicenseFilePath, ct);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex,
                "License reload: failed to read {Path}; previous snapshot retained.", _opts.LicenseFilePath);
            return;
        }

        var hash = SHA256.HashData(bytes);
        if (_lastHash.Length > 0 && hash.AsSpan().SequenceEqual(_lastHash))
        {
            logger.LogDebug("License reload: file unchanged (sha256 match); skipping.");
            return;
        }

        // From here on: the file content has changed. Validate it; on
        // either outcome we still update _lastHash so a same-bad-file
        // doesn't loop indefinitely emitting the same failure event.
        _lastHash = hash;

        var result = await validator.ValidateAsync(bytes, ct);
        if (result.IsValid)
        {
            var previous = provider.Set(result.Snapshot!);
            logger.LogInformation(
                "License reload: snapshot updated to LicenseId {LicenseId} (Tier {Tier}, ValidUntilUtc {ValidUntil:O}); previous {Previous}.",
                result.Snapshot!.LicenseId, result.Snapshot.Tier, result.Snapshot.ValidUntilUtc,
                previous?.LicenseId ?? "<none>");

            await PublishSafeAsync(new LicenseRefreshedIntegrationEvent
            {
                TenantId = PlatformSentinelTenantId,
                LicenseId = result.Snapshot.LicenseId,
                Tier = result.Snapshot.Tier,
                ValidUntilUtc = result.Snapshot.ValidUntilUtc,
                LoadedAtUtc = result.Snapshot.LoadedAtUtc,
            }, ct);
        }
        else
        {
            logger.LogError(
                "License reload: validation FAILED — {ErrorReason}. Previous snapshot retained ({PreviousLicenseId}).",
                result.ErrorReason, provider.Current?.LicenseId ?? "<none>");

            await PublishSafeAsync(new LicenseRefreshFailedIntegrationEvent
            {
                TenantId = PlatformSentinelTenantId,
                ErrorLocalizationKey = result.ErrorLocalizationKey ?? "lockey_licensing_validation_unknown",
                ErrorReason = result.ErrorReason ?? "unknown",
                FailedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            }, ct);
        }
    }

    private async Task PublishSafeAsync<TEvent>(TEvent @event, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        try
        {
            await eventBus.PublishAsync(@event, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Hosted-service boundary — must not bubble out and crash the
            // service for a transient broker hiccup. Snapshot already in
            // memory; admin sees subsequent ticks.
            logger.LogWarning(ex,
                "License reload: failed to publish {EventType}; snapshot state is still consistent locally.",
                typeof(TEvent).Name);
        }
    }

    private PosixSignalRegistration? TryRegisterSignal(PosixSignal signal)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return null;
        try
        {
            return PosixSignalRegistration.Create(signal, ctx =>
            {
                logger.LogInformation("License reload: {Signal} received; interrupting next polling sleep.", signal);
                ctx.Cancel = true; // suppress default-terminate semantics for SIGHUP
                Interlocked.Exchange(ref _interruptCts, null)?.Cancel();
            });
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }
}

/// <summary>
/// DI registration helper — exposes the reload service via
/// <see cref="IHostedService"/>. Called from
/// <c>InfrastructureServiceRegistration</c> alongside the validator and
/// provider singletons.
/// </summary>
public static class LicenseReloadServiceRegistration
{
    /// <summary>Registers the polling-loop reload service + dependencies.</summary>
    public static IServiceCollection AddLicenseReload(
        this IServiceCollection services, Action<LicenseReloadOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);

        services.AddSingleton<IValidateOptions<LicenseReloadOptions>, LicenseReloadOptionsValidator>();
        services.AddSingleton<InMemoryLicenseProvider>();
        services.AddSingleton<ILicenseProvider>(sp => sp.GetRequiredService<InMemoryLicenseProvider>());
        services.AddSingleton<ILicenseValidator, JsonLicenseValidator>();
        services.AddHostedService<LicenseReloadService>();
        return services;
    }
}
