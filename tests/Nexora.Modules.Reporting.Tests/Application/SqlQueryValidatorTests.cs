using Nexora.Modules.Reporting.Application.Services;
using Nexora.Modules.Reporting.Infrastructure.Services;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class SqlQueryValidatorTests
{
    private readonly ISqlQueryValidator _sut = new SqlQueryValidator();

    [Theory]
    [InlineData("SELECT * FROM orders")]
    [InlineData("SELECT COUNT(*) FROM contacts_contacts")]
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte")]
    [InlineData("select id, name from users")]
    public void SqlQueryValidator_WithReadOnlyQueries_ReturnsTrue(string query)
    {
        var result = _sut.IsValid(query, out var error);

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
    public void SqlQueryValidator_WithDmlDdlQueries_ReturnsFalse(string query)
    {
        var result = _sut.IsValid(query, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void SqlQueryValidator_WithSemicolon_ReturnsFalse()
    {
        var result = _sut.IsValid("SELECT 1; SELECT 2", out var error);

        result.Should().BeFalse();
        error.Should().Be("lockey_reporting_validation_query_no_semicolons");
    }

    [Fact]
    public void SqlQueryValidator_WithEmptyQuery_ReturnsFalse()
    {
        var result = _sut.IsValid("", out var error);

        result.Should().BeFalse();
        error.Should().Be("lockey_reporting_validation_query_empty");
    }

    [Fact]
    public void SqlQueryValidator_WithQueryNotStartingWithSelect_ReturnsFalse()
    {
        var result = _sut.IsValid("EXPLAIN SELECT 1", out var error);

        result.Should().BeFalse();
        error.Should().Be("lockey_reporting_validation_query_must_start_select");
    }
}
