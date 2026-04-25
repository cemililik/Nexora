using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Contacts.Tests.Infrastructure.DemoData;

/// <summary>
/// T-029 unit tests for the Contacts module's demo seed (ContactsModule.SeedDemoDataAsync).
/// Asserts row counts per scenario + idempotency on natural-key
/// (lower-cased email) so the orchestrator's marker is the second line
/// of defense, not the first.
/// </summary>
public sealed class ContactsDemoSeedTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly TenantDemoSeedContext _generalCtx;
    private readonly TenantDemoSeedContext _ngoCtx;
    private readonly ServiceProvider _serviceProvider;

    public ContactsDemoSeedTests()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var accessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _dbContext = new ContactsDbContext(options, accessor);

        // Build a tiny ServiceProvider that hands the seed back the SAME
        // _dbContext instance the assertions read from. The orchestrator
        // wraps this in NonOwnedServiceProvider so dispose-on-context is
        // a no-op (matches T-005's lifetime contract).
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        _serviceProvider = services.BuildServiceProvider();

        _generalCtx = new TenantDemoSeedContext(
            TenantId: _tenantId.ToString(),
            SchemaName: $"tenant_{_tenantId}",
            OrganizationId: _orgId.ToString(),
            ScopedServices: new NoOpDisposingProvider(_serviceProvider),
            Scenario: "general");
        _ngoCtx = _generalCtx with { Scenario = "ngo" };
    }

    [Fact]
    public async Task SeedAsync_GeneralScenario_InsertsExpectedContactCount()
    {
        var module = new Nexora.Modules.Contacts.ContactsModule();

        await module.SeedDemoDataAsync(_generalCtx, CancellationToken.None);

        var count = await _dbContext.Contacts.CountAsync();
        count.Should().Be(20, "general scenario seeds 20 business contacts (10 individuals + 10 organisations).");
        // Spot-check both type families landed.
        (await _dbContext.Contacts.CountAsync(c => c.Type == ContactType.Individual))
            .Should().Be(10);
        (await _dbContext.Contacts.CountAsync(c => c.Type == ContactType.Organization))
            .Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_NgoScenario_InsertsExpectedDonorCount()
    {
        var module = new Nexora.Modules.Contacts.ContactsModule();

        await module.SeedDemoDataAsync(_ngoCtx, CancellationToken.None);

        var count = await _dbContext.Contacts.CountAsync();
        count.Should().Be(30, "ngo scenario seeds 30 donor contacts (10 one-time + 10 recurring + 5 lapsed + 5 major).");
        // Spot-check the cohort labels survive into the email local part.
        (await _dbContext.Contacts.CountAsync(c => c.Email!.StartsWith("donor-recurring-")))
            .Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotentOnEmail()
    {
        var module = new Nexora.Modules.Contacts.ContactsModule();

        await module.SeedDemoDataAsync(_generalCtx, CancellationToken.None);
        var firstCount = await _dbContext.Contacts.CountAsync();
        await module.SeedDemoDataAsync(_generalCtx, CancellationToken.None);
        var secondCount = await _dbContext.Contacts.CountAsync();

        secondCount.Should().Be(firstCount,
            "re-running the same scenario must NOT duplicate rows — natural-key (email) lookup short-circuits inserts.");
    }

    [Fact]
    public async Task SeedAsync_UnknownScenario_IsNoOp()
    {
        var module = new Nexora.Modules.Contacts.ContactsModule();
        var unknownCtx = _generalCtx with { Scenario = "totally-not-a-scenario" };

        await module.SeedDemoDataAsync(unknownCtx, CancellationToken.None);

        (await _dbContext.Contacts.CountAsync()).Should().Be(0,
            "modules MAY support a subset of scenarios and skip otherwise — unknown scenario inserts nothing.");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    /// <summary>
    /// Minimal <see cref="INonOwnedServiceProvider"/> shim — the seeder
    /// calls <c>GetRequiredService&lt;ContactsDbContext&gt;()</c> through
    /// it, and dispose is a no-op so the wrapped lifetime is owned by
    /// the test class.
    /// </summary>
    private sealed class NoOpDisposingProvider(IServiceProvider inner) : INonOwnedServiceProvider
    {
        public object? GetService(Type serviceType) => inner.GetService(serviceType);
    }
}
