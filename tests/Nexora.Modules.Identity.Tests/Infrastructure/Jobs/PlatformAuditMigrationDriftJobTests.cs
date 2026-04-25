using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Migrations;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Infrastructure.Jobs;

/// <summary>
/// T-013 integration test for <see cref="PlatformAuditMigrationDriftJob"/>.
/// Seeds drift through a stub <see cref="IModuleMigration"/> + tenants in a
/// fixed point in time, then asserts the job writes the right drift rows
/// and publishes the aggregate event. EF InMemory is sufficient — the
/// job's logic is provider-agnostic and the SQL bits live in
/// <see cref="MigrationRunner"/> + <see cref="IModuleMigration"/> overrides
/// (covered by their own tests).
/// </summary>
public sealed class PlatformAuditMigrationDriftJobTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly MigrationDriftLogDbContext _driftDb;
    private readonly TenantContextAccessor _accessor;
    private readonly IEventBus _eventBus;
    private readonly FixedTimeProvider _time;
    // Frozen clock — 2026-04-25 12:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    public PlatformAuditMigrationDriftJobTests()
    {
        var sharedDbName = Guid.NewGuid().ToString();
        _accessor = new TenantContextAccessor();
        _accessor.SetTenant(Guid.NewGuid().ToString());

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(sharedDbName + "-platform")
            .Options;
        _platformDb = new PlatformDbContext(platformOptions);

        var driftOptions = new DbContextOptionsBuilder<MigrationDriftLogDbContext>()
            .UseInMemoryDatabase(sharedDbName + "-drift")
            .Options;
        _driftDb = new MigrationDriftLogDbContext(driftOptions);

        _eventBus = Substitute.For<IEventBus>();
        _time = new FixedTimeProvider(Now);
    }

    private PlatformAuditMigrationDriftJob CreateJob(IEnumerable<IModuleMigration> moduleMigrations) =>
        new(_accessor, _platformDb, moduleMigrations, _driftDb, _eventBus,
            NullLogger<PlatformAuditMigrationDriftJob>.Instance, _time);

    [Fact]
    public async Task ExecuteAsync_TenantWithDrift_RecordsRow_AndPublishesEvent()
    {
        // Arrange — one tenant, one module that reports drift (different heads).
        var tenant = Tenant.Create("Acme", "acme");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var driftingModule = new StubModuleMigration("crm",
            new MigrationHeadsReport(["20260101000000_Init", "20260201000000_Pipelines"], ["20260101000000_Init"]));

        // Act
        await CreateJob([driftingModule]).RunAsync(
            new PlatformAuditMigrationDriftParams { TenantId = "platform" }, CancellationToken.None);

        // Assert — drift row recorded with both heads.
        var rows = await _driftDb.Drifts.ToListAsync();
        rows.Should().ContainSingle();
        var row = rows[0];
        row.TenantId.Should().Be(tenant.Id.Value);
        row.ModuleName.Should().Be("crm");
        row.KnownHead.Should().Be("20260201000000_Pipelines");
        row.AppliedHead.Should().Be("20260101000000_Init");

        // Aggregate event published with correct counts.
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<MigrationDriftDetectedIntegrationEvent>(e =>
                e.TenantCount == 1 && e.DriftRowCount == 1 && e.AuditCompletedAtUtc == Now.UtcDateTime),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoDrift_NoRowsAndNoEvent()
    {
        var tenant = Tenant.Create("Acme", "acme");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        // Module reports no drift — heads match.
        var aligned = new StubModuleMigration("crm",
            new MigrationHeadsReport(["20260101000000_Init"], ["20260101000000_Init"]));

        await CreateJob([aligned]).RunAsync(
            new PlatformAuditMigrationDriftParams { TenantId = "platform" }, CancellationToken.None);

        (await _driftDb.Drifts.ToListAsync()).Should().BeEmpty();
        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<MigrationDriftDetectedIntegrationEvent>(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_MigrationStartedWithinSuppressionWindow_SkipsTenant()
    {
        // Tenant whose migration started 30 minutes ago — well within the
        // 2-hour rolling-deploy window; drift must be suppressed.
        var tenant = Tenant.Create("Acme", "acme");
        tenant.MarkMigrationStarted(Now.UtcDateTime.AddMinutes(-30));
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var driftingModule = new StubModuleMigration("crm",
            new MigrationHeadsReport(["A", "B"], ["A"]));

        await CreateJob([driftingModule]).RunAsync(
            new PlatformAuditMigrationDriftParams { TenantId = "platform" }, CancellationToken.None);

        (await _driftDb.Drifts.ToListAsync()).Should().BeEmpty();
        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<MigrationDriftDetectedIntegrationEvent>(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_MigrationStartedOutsideSuppressionWindow_RecordsDrift()
    {
        // Tenant whose migration started 3 hours ago — outside the 2h window;
        // drift must NOT be suppressed.
        var tenant = Tenant.Create("Acme", "acme");
        tenant.MarkMigrationStarted(Now.UtcDateTime.AddHours(-3));
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var driftingModule = new StubModuleMigration("crm",
            new MigrationHeadsReport(["A", "B"], ["A"]));

        await CreateJob([driftingModule]).RunAsync(
            new PlatformAuditMigrationDriftParams { TenantId = "platform" }, CancellationToken.None);

        (await _driftDb.Drifts.ToListAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_TerminatedTenant_Excluded()
    {
        var tenant = Tenant.Create("Acme", "acme");
        tenant.Terminate();
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var driftingModule = new StubModuleMigration("crm",
            new MigrationHeadsReport(["A", "B"], ["A"]));

        await CreateJob([driftingModule]).RunAsync(
            new PlatformAuditMigrationDriftParams { TenantId = "platform" }, CancellationToken.None);

        (await _driftDb.Drifts.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ModuleThrows_OtherModulesContinue()
    {
        var tenant = Tenant.Create("Acme", "acme");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var throwingModule = new ThrowingModuleMigration("crm");
        var driftingModule = new StubModuleMigration("documents",
            new MigrationHeadsReport(["A", "B"], ["A"]));

        await CreateJob([throwingModule, driftingModule]).RunAsync(
            new PlatformAuditMigrationDriftParams { TenantId = "platform" }, CancellationToken.None);

        // Drift from the second module is still recorded — the throwing
        // first module did not abort the sweep.
        var rows = await _driftDb.Drifts.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ModuleName.Should().Be("documents");
    }

    public void Dispose()
    {
        _platformDb.Dispose();
        _driftDb.Dispose();
    }

    // --- stubs ---------------------------------------------------------------

    private sealed class StubModuleMigration(string name, MigrationHeadsReport report) : IModuleMigration
    {
        public string ModuleName { get; } = name;
        public Task MigrateAsync(string schemaName, CancellationToken ct = default) => Task.CompletedTask;
        public Task SeedAsync(string schemaName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MigrationHeadsReport> GetMigrationHeadsAsync(string schemaName, CancellationToken ct = default)
            => Task.FromResult(report);
    }

    private sealed class ThrowingModuleMigration(string name) : IModuleMigration
    {
        public string ModuleName { get; } = name;
        public Task MigrateAsync(string schemaName, CancellationToken ct = default) => Task.CompletedTask;
        public Task SeedAsync(string schemaName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MigrationHeadsReport> GetMigrationHeadsAsync(string schemaName, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated module reflection failure");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
