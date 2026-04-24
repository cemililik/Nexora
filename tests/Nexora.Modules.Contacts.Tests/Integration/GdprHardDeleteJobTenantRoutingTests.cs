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
public sealed class GdprHardDeleteJobTenantRoutingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly Guid _tenantAId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

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
        await using var jobDbContext = BuildDbContextRecordingAccess(spy);
        var job = new GdprHardDeleteJob(
            spy, jobDbContext, Substitute.For<IOutbox>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GdprHardDeleteJob>.Instance);

        await job.RunAsync(new GdprHardDeleteParams
        {
            TenantId = _tenantAId.ToString(),
            OrganizationId = _orgId.ToString(),
            ContactId = contactA.Id.Value,
            Reason = "unit test",
            ErasedByUserId = _userId
        }, CancellationToken.None);

        spy.SetTenantCalls.Should().NotBeEmpty(
            "NexoraJob.RunAsync must invoke SetTenant before delegating to ExecuteAsync.");
        spy.FirstCurrentReadAt.Should().NotBeNull(
            "the DbContext under test reads Current at least once during ExecuteAsync — the test would not exercise the contract otherwise.");
        spy.SetTenantCalls.First().Should().BeBefore(spy.FirstCurrentReadAt!.Value,
            "SetTenant MUST be invoked before any DB query inside ExecuteAsync; otherwise schema-per-tenant routing breaks.");
    }

    // --- Helpers ------------------------------------------------------------------

    /// <summary>
    /// <see cref="ITenantContextAccessor"/> double that records the timestamp of
    /// every <c>SetTenant</c> call and the first read of <c>Current</c>. The
    /// SetTenant-before-DB test compares the two so an accidental override that
    /// queries DB before pushing tenant context fails with a clear ordering
    /// violation instead of an obscure schema-not-found error downstream.
    /// </summary>
    private sealed class RecordingTenantContextAccessor : ITenantContextAccessor
    {
        private readonly TenantContextAccessor _inner = new();
        public List<DateTimeOffset> SetTenantCalls { get; } = new();
        public DateTimeOffset? FirstCurrentReadAt { get; private set; }

        public ITenantContext Current
        {
            get
            {
                FirstCurrentReadAt ??= DateTimeOffset.UtcNow;
                return _inner.Current;
            }
        }

        public void SetTenant(string tenantId, string? organizationId = null, string? userId = null)
        {
            SetTenantCalls.Add(DateTimeOffset.UtcNow);
            _inner.SetTenant(tenantId, organizationId, userId);
        }
    }

    /// <summary>
    /// Variant of <see cref="BuildDbContext"/> wired with the recording
    /// accessor so the schema-per-tenant resolution path is identical to the
    /// production-routed call.
    /// </summary>
    private ContactsDbContext BuildDbContextRecordingAccess(ITenantContextAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ContactsDbContext(options, accessor);
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
