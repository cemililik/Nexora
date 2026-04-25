using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.Infrastructure.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Infrastructure.Jobs;

public sealed class PurgeUninstalledModulesJobTests
{
    [Theory]
    // Stable jitter: identical guid input => identical offset across runs.
    // The actual values are irrelevant; what matters is determinism +
    // bounded range [0, 119].
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    public void ComputeJitterOffsetMinutes_DeterministicAndBounded(string guidStr)
    {
        var tid = Guid.Parse(guidStr);
        var first = PurgeUninstalledModulesJob.ComputeJitterOffsetMinutes(tid);
        var second = PurgeUninstalledModulesJob.ComputeJitterOffsetMinutes(tid);
        first.Should().Be(second, "the jitter offset MUST be process-stable");
        first.Should().BeInRange(0, 119);
    }

    [Theory]
    // Both timestamp shapes are accepted per T-026 — implementer choice
    // between contiguous yyyyMMddHHmmss and yyyyMMdd_HHmmss must not break
    // this purge step.
    [InlineData("contacts_persons_del_20250101120000", true)]
    [InlineData("contacts_persons_del_20250101_120000", true)]
    // Snake-case + alphanumeric module/entity slugs are valid.
    [InlineData("crm_lead_pipeline_v2_del_20250101120000", true)]
    // Rejections: missing module prefix, foreign characters, missing _del_
    // marker, malformed timestamps.
    [InlineData("Contacts_Persons_del_20250101120000", false)] // PascalCase
    [InlineData("contacts_persons_del_2025", false)]            // short timestamp
    [InlineData("contacts_persons_20250101120000", false)]      // missing _del_
    [InlineData("contacts_persons_del_2025-01-01T12:00:00", false)] // not numeric
    [InlineData("'; DROP TABLE users; --", false)]              // SQL injection attempt
    [InlineData("", false)]
    public void DeletedTableNameRegex_AcceptsOnlyWhitelistedShape(string candidate, bool expected)
    {
        PurgeUninstalledModulesJob.DeletedTableNameRegex.IsMatch(candidate).Should().Be(expected);
    }

    [Fact]
    public void TenantModule_ParseDeletedTableNames_HandlesCsvAndEmptyAndTrim()
    {
        var tm = TenantModule.Create(TenantId.New(), "contacts");
        tm.ParseDeletedTableNames().Should().BeEmpty();

        tm.RecordUninstall("contacts_persons_del_20250101120000, contacts_addresses_del_20250101120000");
        tm.ParseDeletedTableNames().Should().Equal(
            "contacts_persons_del_20250101120000",
            "contacts_addresses_del_20250101120000");

        tm.SetDeletedTableNames(null);
        tm.ParseDeletedTableNames().Should().BeEmpty();
        tm.DeletedTableNames.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_PerTenant_HardDeletesRow_WhenAllTablesDropped()
    {
        // EF InMemory provider — DropTableAsync's InvalidOperationException
        // catch treats the drop as success so the audit + hard-delete path
        // still runs end-to-end.
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        await using var platformDb = new PlatformDbContext(options);

        var tenantId = Guid.NewGuid();
        var tenantStrong = TenantId.From(tenantId);
        var module = TenantModule.Create(tenantStrong, "contacts");
        module.RecordUninstall("contacts_persons_del_20250101120000");
        platformDb.TenantModules.Add(module);
        await platformDb.SaveChangesAsync();

        // Soft-delete + age the row past retention.
        platformDb.TenantModules.Remove(module);
        await platformDb.SaveChangesAsync();
        var deletedAtCol = platformDb.Entry(module).Property<DateTimeOffset?>("DeletedAt");
        deletedAtCol.CurrentValue = DateTimeOffset.UtcNow.AddDays(-60);
        await platformDb.SaveChangesAsync();

        var resolver = Substitute.For<IConfigurationResolver>();
        resolver.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);
        var auditStore = Substitute.For<IAuditStore>();

        var tenantAccessor = new TenantContextAccessor();
        var job = new PurgeUninstalledModulesJob(
            tenantAccessor, platformDb, resolver, auditStore,
            NullLogger<PurgeUninstalledModulesJob>.Instance);

        await job.RunAsync(
            new PurgeUninstalledModulesParams { TenantId = tenantId.ToString() },
            CancellationToken.None);

        // Row physically gone — even with IgnoreQueryFilters.
        (await platformDb.TenantModules
            .IgnoreQueryFilters()
            .CountAsync(tm => tm.TenantId == tenantStrong)).Should().Be(0);

        // One audit entry written, IsSuccess=true.
        await auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e =>
                e.Module == "Identity" &&
                e.Operation == "module.uninstall.purge" &&
                e.OperationType == OperationType.Delete &&
                e.UserEmail == "system:platform-purge" &&
                e.EntityType == "TenantModule" &&
                e.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PerTenant_KeepsMalformedEntry_AndReportsFailureAudit()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        await using var platformDb = new PlatformDbContext(options);

        var tenantId = Guid.NewGuid();
        var tenantStrong = TenantId.From(tenantId);
        var module = TenantModule.Create(tenantStrong, "contacts");
        // One valid entry + one malformed (e.g. the operator hand-edited the
        // column or a buggy uninstall path wrote a non-conforming string).
        module.RecordUninstall("contacts_persons_del_20250101120000,not-a-valid-table");
        platformDb.TenantModules.Add(module);
        await platformDb.SaveChangesAsync();

        platformDb.TenantModules.Remove(module);
        await platformDb.SaveChangesAsync();
        platformDb.Entry(module).Property<DateTimeOffset?>("DeletedAt").CurrentValue =
            DateTimeOffset.UtcNow.AddDays(-60);
        await platformDb.SaveChangesAsync();

        var resolver = Substitute.For<IConfigurationResolver>();
        resolver.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);
        var auditStore = Substitute.For<IAuditStore>();

        var job = new PurgeUninstalledModulesJob(
            new TenantContextAccessor(), platformDb, resolver, auditStore,
            NullLogger<PurgeUninstalledModulesJob>.Instance);

        await job.RunAsync(
            new PurgeUninstalledModulesParams { TenantId = tenantId.ToString() },
            CancellationToken.None);

        // Row survives because the malformed entry could not be safely dropped.
        var survivor = await platformDb.TenantModules
            .IgnoreQueryFilters()
            .FirstAsync(tm => tm.TenantId == tenantStrong);
        survivor.DeletedTableNames.Should().Be("not-a-valid-table");

        await auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => !e.IsSuccess && e.ErrorKey == "lockey_identity_module_purge_partial"),
            Arg.Any<CancellationToken>());
    }
}
