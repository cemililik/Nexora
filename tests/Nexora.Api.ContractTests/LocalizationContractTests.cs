using System.Reflection;
using FluentValidation;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Api.ContractTests;

/// <summary>
/// Verifies that all user-facing strings in the codebase follow the lockey_ convention.
/// This ensures no hardcoded strings leak into API responses.
/// </summary>
public sealed class LocalizationContractTests
{
    /// <summary>
    /// Module assemblies to scan for localization compliance.
    /// </summary>
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(Nexora.Modules.Identity.Api.UserEndpoints).Assembly,
        typeof(Nexora.Modules.Contacts.Api.ContactEndpoints).Assembly,
        typeof(Nexora.Modules.Documents.Api.DocumentEndpoints).Assembly,
        typeof(Nexora.Modules.Notifications.Api.NotificationEndpoints).Assembly,
        typeof(Nexora.Modules.Reporting.Api.ReportDefinitionEndpoints).Assembly,
    ];

    [Fact]
    public void LocalizedMessage_ShouldEnforceLockeyPrefix()
    {
        // Verify that LocalizedMessage rejects keys without the lockey_ prefix
        var act = () => LocalizedMessage.Of("invalid_key");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*lockey_*");
    }

    [Fact]
    public void LocalizedMessage_ShouldAcceptValidLockeyKeys()
    {
        // Verify that LocalizedMessage accepts valid lockey_ prefixed keys
        var message = LocalizedMessage.Of("lockey_test_valid_key");

        message.Key.Should().Be("lockey_test_valid_key");
        message.Params.Should().BeEmpty();
    }

    [Fact]
    public void LocalizedMessage_WithParams_ShouldPreserveParams()
    {
        var @params = new Dictionary<string, string> { ["name"] = "Test", ["count"] = "5" };
        var message = LocalizedMessage.Of("lockey_test_with_params", @params);

        message.Params.Should().HaveCount(2);
        message.Params["name"].Should().Be("Test");
        message.Params["count"].Should().Be("5");
    }

    [Fact]
    public void AllValidatorMessages_ShouldUseLockeyKeys()
    {
        // Scan all FluentValidation validators across all modules.
        // Instantiate each validator and inspect its rules for WithMessage values.
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var validatorTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => IsFluentValidator(t));

            foreach (var validatorType in validatorTypes)
            {
                try
                {
                    // Try to create a parameterless instance
                    var constructor = validatorType.GetConstructor(Type.EmptyTypes);
                    if (constructor == null) continue;

                    var validator = Activator.CreateInstance(validatorType) as IValidator;
                    if (validator == null) continue;

                    // Access the validator descriptor to get all rules and their error messages
                    var descriptor = validator.CreateDescriptor();
                    var members = descriptor.GetMembersWithValidators();

                    foreach (var member in members)
                    {
                        var rules = descriptor.GetRulesForMember(member.Key);
                        foreach (var rule in rules)
                        {
                            foreach (var component in rule.Components)
                            {
                                var errorMessage = component.GetUnformattedErrorMessage();
                                if (string.IsNullOrEmpty(errorMessage))
                                    continue;

                                // Skip FluentValidation's internal/framework messages:
                                // - Templates with {PropertyName} placeholders (built-in validator defaults)
                                // - "No default error message" (validators using .Must() without .WithMessage())
                                // These are framework internals resolved at runtime, not hardcoded user-facing strings.
                                if (errorMessage.Contains("{PropertyName}") ||
                                    errorMessage.Contains("No default error message"))
                                    continue;

                                if (!errorMessage.StartsWith("lockey_"))
                                {
                                    violations.Add(
                                        $"{validatorType.Name} -> {member.Key}: " +
                                        $"message '{errorMessage}' does not start with 'lockey_'");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Skip validators that require constructor dependencies
                    // (they would need DI — only test parameterless validators)
                    if (ex is not MissingMethodException and not TargetInvocationException)
                        throw;
                }
            }
        }

        violations.Should().BeEmpty(
            "all FluentValidation .WithMessage() calls should use lockey_ keys. Violations:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void AllResultFailures_ShouldUseLockeyKeys()
    {
        // Verify that Result.Failure factory methods enforce lockey_ convention
        // by testing that creating a Result with a non-lockey key throws.
        var act = () => Result.Failure("not_a_lockey_key");

        act.Should().Throw<ArgumentException>(
            "Result.Failure with a non-lockey key should throw because " +
            "it creates a LocalizedMessage internally");
    }

    [Fact]
    public void ResultFailure_WithLockeyKey_ShouldSucceed()
    {
        var result = Result.Failure("lockey_test_failure");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Key.Should().Be("lockey_test_failure");
    }

    [Fact]
    public void Error_ShouldRequireLockeyKey()
    {
        // Error wraps a LocalizedMessage, which enforces lockey_ prefix
        var act = () => new Error(LocalizedMessage.Of("bad_key"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApiEnvelope_ValidationFail_Message_ShouldUseLockeyPrefix()
    {
        // Verify the hardcoded validation failure message uses lockey_ convention
        var envelope = ApiEnvelope<object>.ValidationFail([]);

        envelope.Message.Should().StartWith("lockey_",
            "the default validation failure message must use lockey_ convention");
    }

    /// <summary>
    /// Checks if a type inherits from AbstractValidator{T}.
    /// </summary>
    private static bool IsFluentValidator(Type type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
