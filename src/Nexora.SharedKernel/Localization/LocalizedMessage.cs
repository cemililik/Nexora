namespace Nexora.SharedKernel.Localization;

/// <summary>
/// A localization key with optional parameters.
/// Key MUST start with "lockey_" prefix.
/// </summary>
public sealed record LocalizedMessage
{
    public string Key { get; }
    public Dictionary<string, string> Params { get; }

    public LocalizedMessage(string key, Dictionary<string, string>? @params = null)
    {
        if (!key.StartsWith("lockey_"))
            throw new ArgumentException(
                $"Localization key must start with 'lockey_'. Got: {key}",
                nameof(key));

        Key = key;
        Params = @params ?? [];
    }

    public static LocalizedMessage Of(string key, Dictionary<string, string>? @params = null) =>
        new(key, @params);
}
