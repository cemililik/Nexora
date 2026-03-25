using Nexora.Modules.Reporting.Infrastructure.Services;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class SqlQueryValidatorTests
{
    [Theory]
    [InlineData("SELECT * FROM orders")]
    [InlineData("SELECT COUNT(*) FROM contacts_contacts")]
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte")]
    [InlineData("select id, name from users")]
    public void IsValid_ReadOnlyQueries_ShouldReturnTrue(string query)
    {
        var result = SqlQueryValidator.IsValid(query, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("INSERT INTO orders VALUES (1)")]
    [InlineData("UPDATE orders SET status = 'done'")]
    [InlineData("DELETE FROM orders")]
    [InlineData("DROP TABLE orders")]
    [InlineData("ALTER TABLE orders ADD COLUMN x TEXT")]
    [InlineData("CREATE TABLE evil (id int)")]
    [InlineData("TRUNCATE orders")]
    public void IsValid_DmlDdlQueries_ShouldReturnFalse(string query)
    {
        var result = SqlQueryValidator.IsValid(query, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void IsValid_QueryWithSemicolon_ShouldReturnFalse()
    {
        var result = SqlQueryValidator.IsValid("SELECT 1; SELECT 2", out var error);

        result.Should().BeFalse();
        error.Should().Contain("Semicolons");
    }

    [Fact]
    public void IsValid_EmptyQuery_ShouldReturnFalse()
    {
        var result = SqlQueryValidator.IsValid("", out var error);

        result.Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Fact]
    public void IsValid_QueryNotStartingWithSelect_ShouldReturnFalse()
    {
        var result = SqlQueryValidator.IsValid("EXPLAIN SELECT 1", out var error);

        result.Should().BeFalse();
        error.Should().Contain("SELECT or WITH");
    }
}
