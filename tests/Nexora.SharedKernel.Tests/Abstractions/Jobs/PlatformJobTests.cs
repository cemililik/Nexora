using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.SharedKernel.Tests.Abstractions.Jobs;

// --- Test doubles ---

public sealed record TestJobParams : JobParams;

/// <summary>
/// Concrete PlatformJob for testing. Tracks which tenants were processed
/// and allows injecting failures for specific tenants.
/// </summary>
public sealed class TestPlatformJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<TestPlatformJob> logger,
    string? requiredModule = null) : PlatformJob<TestJobParams>(tenantProvider, scopeFactory, logger)
{
    private readonly string? _requiredModule = requiredModule;

    public List<string> ProcessedTenants { get; } = [];
    public HashSet<string> FailingTenants { get; } = [];

    protected override string? GetRequiredModule() => _requiredModule;

    protected override Task ExecuteForTenantAsync(
        TestJobParams parameters,
        ActiveTenantInfo tenant,
        IServiceProvider scopedServices,
        CancellationToken ct)
    {
        if (FailingTenants.Contains(tenant.TenantId))
            throw new InvalidOperationException($"Simulated failure for {tenant.TenantId}");

        ProcessedTenants.Add(tenant.TenantId);
        return Task.CompletedTask;
    }
}

public sealed class PlatformJobTests
{
    private readonly IActiveTenantProvider _tenantProvider = Substitute.For<IActiveTenantProvider>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly ILogger<TestPlatformJob> _logger = Substitute.For<ILogger<TestPlatformJob>>();

    private void SetupScope()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var tenantAccessor = Substitute.For<ITenantContextAccessor>();

        serviceProvider.GetService(typeof(ITenantContextAccessor)).Returns(tenantAccessor);
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory.CreateScope().Returns(scope);
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));
    }

    [Fact]
    public async Task RunAsync_IteratesAllTenants()
    {
        // Arrange
        var tenants = new List<ActiveTenantInfo>
        {
            new("tenant-1", "tenant_tenant-1"),
            new("tenant-2", "tenant_tenant-2"),
            new("tenant-3", "tenant_tenant-3")
        };

        _tenantProvider.GetActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tenants.AsReadOnly());

        SetupScope();

        var job = new TestPlatformJob(_tenantProvider, _scopeFactory, _logger);
        var parameters = new TestJobParams { TenantId = "platform" };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        job.ProcessedTenants.Should().HaveCount(3);
        job.ProcessedTenants.Should().ContainInOrder("tenant-1", "tenant-2", "tenant-3");
    }

    [Fact]
    public async Task RunAsync_OneTenantFails_ContinuesOthers()
    {
        // Arrange
        var tenants = new List<ActiveTenantInfo>
        {
            new("tenant-1", "tenant_tenant-1"),
            new("tenant-fail", "tenant_tenant-fail"),
            new("tenant-3", "tenant_tenant-3")
        };

        _tenantProvider.GetActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tenants.AsReadOnly());

        SetupScope();

        var job = new TestPlatformJob(_tenantProvider, _scopeFactory, _logger);
        job.FailingTenants.Add("tenant-fail");

        var parameters = new TestJobParams { TenantId = "platform" };

        // Act — should NOT throw even though one tenant fails
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert — tenant-1 and tenant-3 were processed, tenant-fail was skipped
        job.ProcessedTenants.Should().HaveCount(2);
        job.ProcessedTenants.Should().Contain("tenant-1");
        job.ProcessedTenants.Should().Contain("tenant-3");
        job.ProcessedTenants.Should().NotContain("tenant-fail");
    }

    [Fact]
    public async Task RunAsync_WithModuleFilter_FiltersToCorrectTenants()
    {
        // Arrange
        var moduleTenants = new List<ActiveTenantInfo>
        {
            new("tenant-crm-1", "tenant_tenant-crm-1"),
            new("tenant-crm-2", "tenant_tenant-crm-2")
        };

        _tenantProvider.GetActiveTenantsWithModuleAsync("crm", Arg.Any<CancellationToken>())
            .Returns(moduleTenants.AsReadOnly());

        SetupScope();

        var job = new TestPlatformJob(_tenantProvider, _scopeFactory, _logger, requiredModule: "crm");
        var parameters = new TestJobParams { TenantId = "platform" };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _tenantProvider.Received(1).GetActiveTenantsWithModuleAsync("crm", Arg.Any<CancellationToken>());
        await _tenantProvider.DidNotReceive().GetActiveTenantsAsync(Arg.Any<CancellationToken>());
        job.ProcessedTenants.Should().HaveCount(2);
        job.ProcessedTenants.Should().ContainInOrder("tenant-crm-1", "tenant-crm-2");
    }

    [Fact]
    public async Task RunAsync_LogsSuccessAndFailureCounts()
    {
        // Arrange
        var tenants = new List<ActiveTenantInfo>
        {
            new("tenant-ok", "tenant_tenant-ok"),
            new("tenant-bad", "tenant_tenant-bad")
        };

        _tenantProvider.GetActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tenants.AsReadOnly());

        SetupScope();

        var job = new TestPlatformJob(_tenantProvider, _scopeFactory, _logger);
        job.FailingTenants.Add("tenant-bad");

        var parameters = new TestJobParams { TenantId = "platform" };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert — verify specific log calls for starting, per-tenant error, and finish summary
        var logCalls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Select(c => new
            {
                Level = (LogLevel)c.GetArguments()[0]!,
                Message = c.GetArguments()[2]?.ToString() ?? ""
            })
            .ToList();

        logCalls.Should().HaveCountGreaterThanOrEqualTo(4); // starting + processing count + error + finished
        logCalls[0].Level.Should().Be(LogLevel.Information);
        logCalls[0].Message.Should().Contain("starting");
        logCalls[1].Level.Should().Be(LogLevel.Information);
        logCalls[1].Message.Should().Contain("2 tenants");

        // The error log for the failing tenant
        logCalls.Should().Contain(l => l.Level == LogLevel.Error && l.Message.Contains("tenant-bad"));

        // The finish log with success/failure counts
        var finishLog = logCalls.Last();
        finishLog.Level.Should().Be(LogLevel.Information);
        finishLog.Message.Should().Contain("1 succeeded");
        finishLog.Message.Should().Contain("1 failed");
    }

    [Fact]
    public async Task RunAsync_WithNoTenants_CompletesWithoutError()
    {
        // Arrange
        _tenantProvider.GetActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ActiveTenantInfo>().AsReadOnly());

        SetupScope();

        var job = new TestPlatformJob(_tenantProvider, _scopeFactory, _logger);
        var parameters = new TestJobParams { TenantId = "platform" };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        job.ProcessedTenants.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithoutModuleFilter_CallsGetActiveTenantsAsync()
    {
        // Arrange
        _tenantProvider.GetActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ActiveTenantInfo>().AsReadOnly());

        SetupScope();

        var job = new TestPlatformJob(_tenantProvider, _scopeFactory, _logger);
        var parameters = new TestJobParams { TenantId = "platform" };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _tenantProvider.Received(1).GetActiveTenantsAsync(Arg.Any<CancellationToken>());
        await _tenantProvider.DidNotReceive().GetActiveTenantsWithModuleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
