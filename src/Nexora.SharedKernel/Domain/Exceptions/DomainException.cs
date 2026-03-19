namespace Nexora.SharedKernel.Domain.Exceptions;

/// <summary>
/// Domain invariant violation. Message MUST be a lockey_ key.
/// </summary>
public sealed class DomainException : Exception
{
    public string LocalizationKey { get; }
    public Dictionary<string, string> Params { get; }

    public DomainException(string localizationKey, Dictionary<string, string>? @params = null)
        : base(localizationKey)
    {
        if (!localizationKey.StartsWith("lockey_"))
            throw new ArgumentException(
                $"Domain exception message must be a lockey_ key. Got: {localizationKey}",
                nameof(localizationKey));

        LocalizationKey = localizationKey;
        Params = @params ?? [];
    }
}
