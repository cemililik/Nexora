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

    // --- Helpers ------------------------------------------------------------------

    private async Task CreateSchemaAsync(string schema)
    {
        // Npgsql quotes identifiers with double-quotes; we build the SQL ourselves
        // because EnsureCreated does NOT create the schema itself.
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
        // skip it).
        //
        // KNOWN PRE-EXISTING BUG (out of T-018 scope): two Contacts configurations
        // declare `HasFilter("\"IsDeleted\" = false")` on entities that don't extend
        // AuditableEntity and therefore have no IsDeleted column
        // (ContactTagConfiguration, ContactCustomFieldConfiguration — both dating back
        // to commit 006a8ea "feat: implement global soft delete infrastructure").
        // Dev works because the live schema pre-dates the filter and its table shape
        // has been patched by raw SQL since; a fresh Testcontainers Postgres exposes
        // the drift.
        //
        // `IRelationalDatabaseCreator.CreateTablesAsync` wraps everything in one
        // transaction, so the single bad CREATE INDEX rolls the tables back too.
        // Execute the DDL script statement-by-statement outside a transaction so the
        // broken-index statements can fail in isolation without rolling back tables.
        // Follow-up task will remove the stray HasFilter calls.
        var script = db.Database.GenerateCreateScript();
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        foreach (var statement in SplitDdlStatements(script))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = statement;
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42703")
            {
                // column missing on a HasFilter'd partial index — skip.
            }
        }

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

    /// <summary>
    /// Splits EF's generated create script into individual statements. EF wraps some
    /// statements in dollar-quoted blocks like <c>$EF$ ... $EF$</c>; a naive semicolon
    /// split tears those apart. We walk the string tracking whether we are inside a
    /// dollar-quoted region so the splitter only acts on top-level statement
    /// terminators.
    /// </summary>
    private static IEnumerable<string> SplitDdlStatements(string script)
    {
        var sb = new System.Text.StringBuilder();
        string? openTag = null;
        int i = 0;
        while (i < script.Length)
        {
            if (openTag is null)
            {
                // Look for a dollar-quote open tag (e.g. $EF$, $$, $tag$).
                if (script[i] == '$')
                {
                    var end = script.IndexOf('$', i + 1);
                    if (end > i)
                    {
                        var candidate = script.Substring(i, end - i + 1);
                        // A valid tag is $...$ where the inner is an identifier or empty.
                        if (IsValidDollarTag(candidate))
                        {
                            sb.Append(candidate);
                            openTag = candidate;
                            i = end + 1;
                            continue;
                        }
                    }
                }
                if (script[i] == ';' && (i + 1 >= script.Length || script[i + 1] == '\n' || script[i + 1] == '\r'))
                {
                    var stmt = sb.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(stmt))
                        yield return stmt;
                    sb.Clear();
                    i++;
                    continue;
                }
            }
            else if (i + openTag.Length <= script.Length &&
                     script.Substring(i, openTag.Length) == openTag)
            {
                sb.Append(openTag);
                i += openTag.Length;
                openTag = null;
                continue;
            }

            sb.Append(script[i]);
            i++;
        }

        var tail = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            yield return tail;
    }

    private static bool IsValidDollarTag(string candidate)
    {
        if (candidate.Length < 2 || candidate[0] != '$' || candidate[^1] != '$') return false;
        var inner = candidate[1..^1];
        if (inner.Length == 0) return true;
        if (!char.IsLetter(inner[0]) && inner[0] != '_') return false;
        foreach (var c in inner)
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        return true;
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

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
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
