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

    [Theory]
    [InlineData("SELECT * FROM t WHERE DELETE = 1")]
    [InlineData("SELECT * FROM t UNION DROP TABLE orders")]
    [InlineData("SELECT id FROM t WHERE INSERT = 'x'")]
    public void SqlQueryValidator_WithForbiddenKeywordInSelectQuery_ReturnsForbiddenKeywordError(string query)
    {
        var result = _sut.IsValid(query, out var error);

        result.Should().BeFalse();
        error.Should().Be("lockey_reporting_validation_query_forbidden_keyword");
    }

    [Theory]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT dblink('conn', 'SELECT 1')")]
    [InlineData("SELECT lo_import('/etc/passwd')")]
    [InlineData("SELECT lo_export(1, '/tmp/out')")]
    [InlineData("SELECT pg_execute_server_program('ls')")]
    public void SqlQueryValidator_WithForbiddenFunction_ReturnsForbiddenFunctionError(string query)
    {
        var result = _sut.IsValid(query, out var error);

        result.Should().BeFalse();
        error.Should().Be("lockey_reporting_validation_query_forbidden_function");
    }

    [Theory]
    [InlineData("/* DELETE FROM users */ SELECT 1")]
    [InlineData("/* DROP TABLE orders */ SELECT id FROM contacts")]
    [InlineData("-- DELETE FROM users\nSELECT * FROM orders")]
    [InlineData("SELECT id FROM contacts -- DROP TABLE t")]
    [InlineData("/* pg_read_file('/etc/passwd') */ SELECT 1")]
    [InlineData("SELECT id FROM t /* INSERT INTO t VALUES(1) */ WHERE id = 1")]
    public void SqlQueryValidator_WithForbiddenKeywordOnlyInComment_ReturnsTrue(string query)
    {
        var result = _sut.IsValid(query, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }
}
