using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Nexora.Modules.Contacts.Tests.Integration;

/// <summary>
/// xUnit fixture that owns the shared <see cref="PostgreSqlContainer"/>
/// across every test in <see cref="GdprHardDeleteJobTenantRoutingTests"/>.
/// Container startup is the single most expensive line in the suite (~5 s
/// on a warm runner); paying it once per class instead of once per [Fact]
/// keeps the integration test runtime bounded as more cases are added.
///
/// <para>
/// Tenant / org / user GUIDs are NOT held on the fixture — each test gets
/// fresh GUIDs via the test class constructor (xUnit constructs a new
/// test instance per [Fact]). Sharing those state guids across tests would
/// reuse the same per-tenant schemas and trip "relation already exists"
/// errors on the second test's <c>CreateTablesAsync</c>.
/// </para>
/// </summary>
public sealed class GdprHardDeleteJobTenantRoutingFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => Postgres.StartAsync();

    public Task DisposeAsync() => Postgres.DisposeAsync().AsTask();
}

/// <summary>
/// T-018 AC#1: proves <see cref="GdprHardDeleteJob"/> routes reads and writes to the
/// tenant schema that matches the tenant context set by <c>NexoraJob.RunAsync</c>,
/// and that another tenant's data in a sibling schema is untouched.
///
/// Uses a real Postgres 17 container so <c>HasDefaultSchema</c> + Npgsql's
/// <c>search_path</c> round-trip are exercised end-to-end — the <c>InMemory</c>
/// provider used elsewhere in the module's tests cannot catch schema-routing bugs
/// because it has no concept of schemas.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GdprHardDeleteJobTenantRoutingTests(GdprHardDeleteJobTenantRoutingFixture fixture)
    : IClassFixture<GdprHardDeleteJobTenantRoutingFixture>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = fixture.Postgres;
    // Per-test GUIDs (xUnit constructs a new test instance per [Fact]).
    // Each test's schemas live for the test's lifetime and are dropped in
    // DisposeAsync so the shared container's state stays clean — without
    // the cleanup, the EF model cache (keyed on schema) holds onto the
    // previous test's HasDefaultSchema even after the test class instance
    // dies, and CreateTablesAsync attempts to recreate tables in the
    // same physical schema → 42P07 "relation already exists".
    private readonly Guid _tenantAId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Drop the per-test tenant schemas so the next [Fact] starts clean.
        // Identifier-safe: tenant_<guid> shape is generated inside this class,
        // never from external input.
        //
        // Per-tenant try/catch: a failure dropping tenant A must NOT short-circuit
        // the drop for tenant B. Without per-iteration isolation, a flaky teardown
        // on the first schema would leave the second one dangling and the NEXT
        // test would hit 42P07 "relation already exists" on CreateTablesAsync —
        // turning one transient error into a cascading failure across the suite.
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        foreach (var tid in new[] { _tenantAId, _tenantBId })
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"DROP SCHEMA IF EXISTS \"tenant_{tid}\" CASCADE";
                await cmd.ExecuteNonQueryAsync();
            }
            catch (PostgresException)
            {
                // Teardown best-effort — surface in test output but keep going.
            }
        }
    }

    [Fact]
    public async Task GdprHardDeleteJob_RunsUnderTenantA_OnlyTouchesTenantASchema()
    {
        // Arrange — two tenant schemas, one contact in each.
        var schemaA = $"tenant_{_tenantAId}";
        var schemaB = $"tenant_{_tenantBId}";
        await CreateSchemaAsync(schemaA);
        await CreateSchemaAsync(schemaB);

        var contactA = await SeedContactAsync(_tenantAId);
        var contactB = await SeedContactAsync(_tenantBId);

        // Act — run the job under tenant A's context, just like NexoraJob.RunAsync would.
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantAId.ToString(), _orgId.ToString(), _userId.ToString());

        await using var jobDbContext = BuildDbContext(accessor);
        var outbox = Substitute.For<IOutbox>();
        var job = new GdprHardDeleteJob(
            accessor, jobDbContext, outbox, NullLogger<GdprHardDeleteJob>.Instance);

        await job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantAId.ToString(),
            OrganizationId = _orgId.ToString(),
            ContactId = contactA.Id.Value,
            Reason = "unit test",
            ErasedByUserId = _userId
        }, CancellationToken.None);

        // Assert — tenant A's contact is gone, tenant B's contact is intact.
        (await ContactExistsAsync(_tenantAId, contactA.Id)).Should().BeFalse(
            "tenant A's contact must be permanently erased from tenant_A schema");
        (await ContactExistsAsync(_tenantBId, contactB.Id)).Should().BeTrue(
            "tenant B's contact must survive — the job must not cross tenant boundaries");

        // And the outbox was handed an integration event with the correct tenant id.
        await outbox.Received(1).EnqueueAsync(
            Arg.Is<ContactGdprDeletedIntegrationEvent>(e =>
                e.TenantId == _tenantAId.ToString() && e.ContactId == contactA.Id.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GdprHardDeleteJob_RunAsync_LogsTenantContextBeforeExecute()
    {
        // Regression guard for T-018 AC#2: the base NexoraJob must log "Job ... starting
        // for tenant {TenantId}" BEFORE any DB query in ExecuteAsync. A test logger
        // captures log events and asserts the start-log precedes the completion-log.
        var schemaA = $"tenant_{_tenantAId}";
        await CreateSchemaAsync(schemaA);
        var contactA = await SeedContactAsync(_tenantAId);

        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantAId.ToString(), _orgId.ToString(), _userId.ToString());

        var logger = new CapturingLogger<GdprHardDeleteJob>();
        await using var jobDbContext = BuildDbContext(accessor);
        var job = new GdprHardDeleteJob(
            accessor, jobDbContext, Substitute.For<IOutbox>(), logger);

        await job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantAId.ToString(),
            OrganizationId = _orgId.ToString(),
            ContactId = contactA.Id.Value,
            Reason = "unit test",
            ErasedByUserId = _userId
        }, CancellationToken.None);

        var startIdx = logger.Messages.FindIndex(m => m.Contains("starting for tenant"));
        var completeIdx = logger.Messages.FindIndex(m => m.Contains("completed for tenant"));

        startIdx.Should().BeGreaterOrEqualTo(0, "NexoraJob.RunAsync must emit a starting-for-tenant log");
        completeIdx.Should().BeGreaterThan(startIdx,
            "the completion log must come AFTER the starting log — tenant context is set before any DB query in between");
        logger.Messages[startIdx].Should().Contain(_tenantAId.ToString(),
            "the starting log must carry the tenant id so operators can correlate by tenant");
    }

    [Fact]
    public async Task GdprHardDeleteJob_RunAsync_CallsSetTenantBeforeAnyDbAccess()
    {
        // T-018 AC#2 — explicit version: a spy accessor records the order in
        // which SetTenant is invoked vs. when the wrapped DbContext first
        // observes Current. The contract is that NexoraJob.RunAsync MUST set
        // tenant context before any DB query inside ExecuteAsync; without that,
        // the schema-per-tenant resolution falls back to "default" and rows
        // land in the wrong tenant.
        var schemaA = $"tenant_{_tenantAId}";
        await CreateSchemaAsync(schemaA);
        var contactA = await SeedContactAsync(_tenantAId);

        var spy = new RecordingTenantContextAccessor();
        await using var jobDbContext = BuildDbContext(spy);
        var job = new GdprHardDeleteJob(
            spy, jobDbContext, Substitute.For<IOutbox>(),
            NullLogger<GdprHardDeleteJob>.Instance);

        await job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantAId.ToString(),
            OrganizationId = _orgId.ToString(),
            ContactId = contactA.Id.Value,
            Reason = "unit test",
            ErasedByUserId = _userId
        }, CancellationToken.None);

        spy.SetTenantCallSequences.Should().NotBeEmpty(
            "NexoraJob.RunAsync must invoke SetTenant before delegating to ExecuteAsync.");
        spy.FirstCurrentReadSequence.Should().NotBeNull(
            "the DbContext under test reads Current at least once during ExecuteAsync — the test would not exercise the contract otherwise.");
        spy.SetTenantCallSequences.First().Should().BeLessThan(spy.FirstCurrentReadSequence!.Value,
            "SetTenant MUST be invoked before any DB query inside ExecuteAsync; otherwise schema-per-tenant routing breaks.");
    }

    // --- Helpers ------------------------------------------------------------------

    /// <summary>
    /// <see cref="ITenantContextAccessor"/> double that records a strictly
    /// monotonic sequence number on every <c>SetTenant</c> call and on the
    /// first read of <c>Current</c>. The SetTenant-before-DB test compares
    /// the two sequence numbers — sequence comparison is deadlock-free and
    /// cannot flake on coarse system-clock granularity (Windows
    /// DateTimeOffset.UtcNow ticks at ~15 ms, so two events fired within
    /// the same tick used to compare equal under the old timestamp-based
    /// implementation).
    /// </summary>
    private sealed class RecordingTenantContextAccessor : ITenantContextAccessor
    {
        private readonly TenantContextAccessor _inner = new();
        private long _sequence;
        public List<long> SetTenantCallSequences { get; } = new();
        public long? FirstCurrentReadSequence { get; private set; }

        public ITenantContext Current
        {
            get
            {
                FirstCurrentReadSequence ??= System.Threading.Interlocked.Increment(ref _sequence);
                return _inner.Current;
            }
        }

        public void SetTenant(string tenantId, string? organizationId = null, string? userId = null)
        {
            SetTenantCallSequences.Add(System.Threading.Interlocked.Increment(ref _sequence));
            _inner.SetTenant(tenantId, organizationId, userId);
        }
    }

    /// <summary>
    /// Creates the named schema if missing. **SECURITY**: this helper interpolates
    /// <paramref name="schema"/> directly into a CREATE SCHEMA statement because
    /// Postgres does not parameterize identifiers. The helper accepts ONLY
    /// identifier-safe values produced inside this test class (the
    /// <c>tenant_{Guid}</c> shape from <see cref="_tenantAId"/> / <see cref="_tenantBId"/>);
    /// it MUST NOT be called with untrusted input. If this helper ever moves out
    /// of the test assembly, validate or quote-escape the parameter at the new
    /// boundary.
    /// </summary>
    private async Task CreateSchemaAsync(string schema)
    {
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<Contact> SeedContactAsync(Guid tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        await using var db = BuildDbContext(accessor);
        // Drive Npgsql's RelationalDatabaseCreator directly — this mirrors what
        // DevelopmentSeed.EnsureModuleTablesAsync does in dev (EnsureCreatedAsync
        // wants a migrations history table + trips on global query filters, so we
        // skip it). T-021 removed the pre-existing HasFilter drift on
        // ContactTag / ContactCustomField, so CreateTablesAsync now runs end-to-end
        // in its single transaction without any column-missing failures.
        var creator = db.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        var contact = Contact.Create(
            tenantId: tenantId,
            organizationId: _orgId,
            type: ContactType.Individual,
            firstName: "Jane",
            lastName: "Doe",
            companyName: null,
            email: $"jane+{tenantId:N}@example.com",
            phone: null,
            source: ContactSource.Manual);

        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    private async Task<bool> ContactExistsAsync(Guid tenantId, ContactId contactId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        await using var db = BuildDbContext(accessor);
        return await db.Contacts
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Id == contactId);
    }

    private ContactsDbContext BuildDbContext(ITenantContextAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ContactsDbContext(options, accessor);
    }

    /// <summary>
    /// Minimal capturing logger — records formatted messages so the test can assert
    /// ordering between "starting" and "completed" log lines without pulling in a
    /// third-party logger mock.
    /// </summary>
    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = new();

        // Match Microsoft.Extensions.Logging.ILogger.BeginScope's nullable
        // return so the implementation lines up with .NET 9+ NRT signatures.
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullDisposable.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
