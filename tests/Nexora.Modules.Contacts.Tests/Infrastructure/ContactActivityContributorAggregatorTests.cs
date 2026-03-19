using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Contacts.Tests.Infrastructure;

public sealed class ContactActivityContributorAggregatorTests
{
    [Fact]
    public async Task GetAllSummariesAsync_WithContributors_ShouldReturnAll()
    {
        // Arrange
        var contributors = new IContactActivityContributor[]
        {
            new TestContributor("crm", new ModuleContactSummary("crm", "CRM", new Dictionary<string, object?> { ["deals"] = 3 })),
            new TestContributor("donations", new ModuleContactSummary("donations", "Donations", new Dictionary<string, object?> { ["total"] = 500 }))
        };
        var aggregator = new ContactActivityContributorAggregator(contributors);

        // Act
        var result = await aggregator.GetAllSummariesAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllSummariesAsync_NullContributor_ShouldSkip()
    {
        // Arrange
        var contributors = new IContactActivityContributor[]
        {
            new TestContributor("crm", new ModuleContactSummary("crm", "CRM", new Dictionary<string, object?> { ["deals"] = 3 })),
            new TestContributor("empty", null)
        };
        var aggregator = new ContactActivityContributorAggregator(contributors);

        // Act
        var result = await aggregator.GetAllSummariesAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ModuleName.Should().Be("crm");
    }

    [Fact]
    public async Task GetAllSummariesAsync_NoContributors_ShouldReturnEmpty()
    {
        // Arrange
        var aggregator = new ContactActivityContributorAggregator([]);

        // Act
        var result = await aggregator.GetAllSummariesAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private sealed class TestContributor(string moduleName, ModuleContactSummary? summary) : IContactActivityContributor
    {
        public string ModuleName => moduleName;
        public Task<ModuleContactSummary?> GetSummaryAsync(Guid contactId, Guid organizationId, CancellationToken ct)
            => Task.FromResult(summary);
    }
}
