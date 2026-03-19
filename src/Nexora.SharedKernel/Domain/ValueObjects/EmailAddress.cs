using System.Text.RegularExpressions;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Value object for validated email addresses.
/// </summary>
public sealed partial record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !EmailRegex().IsMatch(value))
            throw new DomainException("lockey_shared_email_invalid");

        Value = value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
