using Nexora.Infrastructure.Migrations;

namespace Nexora.Infrastructure.Tests.Migrations;

/// <summary>
/// T-011: orchestration-shape unit tests for the parts of
/// <see cref="MigrationRunner"/> that don't need a real Postgres
/// connection. The advisory-lock + tenant-status-update + actual module
/// migration paths are covered by the Testcontainers-backed
/// <c>MigrationRunnerIntegrationTests</c> (Category="Integration") so
/// they are skipped by the default PR CI.
/// </summary>
public sealed class MigrationRunnerTests
{
    [Fact]
    public void ComputeLockKey_SameTenantId_ReturnsSameKey()
    {
        // The lock key MUST be stable across calls (and across processes
        // — see ADR-0027 implementation rationale) so two
        // platform:migrate-tenants invocations against the same tenant
        // hit the same advisory-lock slot. FNV-1a-64 over the same
        // input must produce the same hash byte-for-byte.
        var tenantId = Guid.Parse("3f2bc7a1-1234-5678-9abc-def012345678");

        var first = MigrationRunner.ComputeLockKey(tenantId);
        var second = MigrationRunner.ComputeLockKey(tenantId);

        first.Should().Be(second,
            "lock key must be deterministic per tenant — drift would let two runners enter the same migration concurrently.");
    }

    [Fact]
    public void ComputeLockKey_DifferentTenantIds_ReturnsDistinctKeys()
    {
        // Distinct tenants must hit distinct lock slots; otherwise
        // tenant A migration would block tenant B migration unnecessarily,
        // capping fleet-wide migration throughput.
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");

        MigrationRunner.ComputeLockKey(a).Should().NotBe(
            MigrationRunner.ComputeLockKey(b),
            "two distinct tenants must not collide on the advisory lock — would serialise unrelated migrations.");
    }

    [Fact]
    public void MigrationFailure_Create_CapturesExceptionShape()
    {
        var tenantId = Guid.NewGuid();
        var ex = new InvalidOperationException("simulated migration failure");

        var failure = MigrationFailure.Create(tenantId, "contacts", ex);

        failure.TenantId.Should().Be(tenantId);
        failure.ModuleName.Should().Be("contacts");
        failure.ExceptionType.Should().Be("System.InvalidOperationException");
        failure.ExceptionMessage.Should().Be("simulated migration failure");
        failure.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MigrationFailure_Truncate_CutsOversizedInputToColumnCap()
    {
        // The StackTrace column is varchar(StackTraceMaxLength) — anything
        // larger gets truncated at the factory so the failure-log INSERT
        // never raises a column-overflow error in the very path that
        // exists to record the original failure. Real exceptions rarely
        // overflow the cap, so we exercise Truncate directly with a
        // synthetic over-cap input — the previous test passed even when
        // truncation was a no-op because the synthetic stack trace was a
        // few hundred chars (review #37).
        var oversized = new string('x', MigrationFailure.StackTraceMaxLength + 5_000);

        var truncated = MigrationFailure.Truncate(oversized, MigrationFailure.StackTraceMaxLength);

        truncated.Should().NotBeNull();
        truncated!.Length.Should().Be(MigrationFailure.StackTraceMaxLength,
            "Truncate must clamp inputs longer than the column cap");
        truncated.Should().Be(oversized[..MigrationFailure.StackTraceMaxLength]);
    }

    [Fact]
    public void MigrationFailure_Truncate_LeavesShorterInputUnchanged()
    {
        var input = "short stack trace";
        MigrationFailure.Truncate(input, MigrationFailure.StackTraceMaxLength)
            .Should().Be(input);
        MigrationFailure.Truncate(null, MigrationFailure.StackTraceMaxLength)
            .Should().BeNull();
    }
}
